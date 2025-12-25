// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

// P/Invoke requires exact native API naming (MEMORYSTATUSEX, sysctlbyname, etc.)
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LumaCore.Core.Diagnostics;

/// <summary>
/// Factory for creating <see cref="MemoryMetrics"/> snapshots containing managed,
/// process-level, and optional system-level memory metrics.
/// </summary>
/// <remarks>
///     <para>
///     See <see cref="MemoryMetrics"/> for detailed documentation on each metric and diagnostic tips.
///     </para>
///     <para>
///     This class intentionally exposes raw telemetry values without attempting to derive a strict managed vs. native
///     memory split, which is not reliably possible using cross-platform BCL APIs alone.
///     </para>
///     <para>
///     The produced values are intended for diagnostics and operational visibility, not for precise profiling.
///     Interpretation of these values must always consider the scope they originate from:
///     </para>
///     <list type="bullet">
///         <item>Managed / GC view: What the .NET runtime sees and manages.</item>
///         <item>
///         Process view: What the operating system attributes to the current process (managed + unmanaged combined).
///         </item>
///         <item>System view (optional): Host-level memory availability, if detectable.</item>
///     </list>
///     <para>
///     System-level values are OS-specific and may be unavailable on some platforms (e.g., macOS) without native APIs.
///     In those cases, <see langword="null"/> values are returned intentionally.
///     </para>
/// </remarks>
public static class MemoryMetricsFactory
{
	/// <summary>
	/// Creates a snapshot of current memory usage and availability.
	/// </summary>
	/// <returns>
	/// A <see cref="MemoryMetrics"/> instance containing raw memory telemetry values.
	/// </returns>
	public static MemoryMetrics Create()
	{
		// ---------------------------------------------------------------------
		// Managed / GC view
		// ---------------------------------------------------------------------
		// GC.GetTotalMemory(false) returns an approximation of the memory
		// currently occupied by live managed objects.
		// No full GC is forced to avoid disturbing runtime behavior.
		long managedLiveBytes = GC.GetTotalMemory(forceFullCollection: false);

		// GCMemoryInfo exposes additional GC-related metrics such as:
		// - Heap size
		// - Fragmentation
		// - Pinned objects
		// - Container-aware available memory
		GCMemoryInfo gc = GC.GetGCMemoryInfo();

		// ---------------------------------------------------------------------
		// Process view (managed + native mixed)
		// ---------------------------------------------------------------------
		// These values reflect what the operating system attributes to the
		// current process. They include managed heap, native allocations,
		// runtime overhead, thread stacks, and loaded native libraries.
		using var process = Process.GetCurrentProcess();

		long workingSetBytes = process.WorkingSet64;
		long privateBytes = process.PrivateMemorySize64;

		// ---------------------------------------------------------------------
		// System view (optional, OS-specific)
		// ---------------------------------------------------------------------
		// Host-level memory information is inherently platform-specific.
		// If it cannot be retrieved reliably, null is returned intentionally.
		SystemMemoryInfo? systemMemory = TryGetSystemMemory();

		// Container view (Linux cgroups)
		// ---------------------------------------------------------------------
		// If running in a container with a memory limit, read the cgroup hard limit.
		long? containerLimit = TryGetContainerMemoryLimit();
		long? containerUsage = TryGetContainerMemoryUsage();

		// Effective limit = min(container, system) — the real ceiling
		long? effectiveLimit = (containerLimit, systemMemory?.TotalPhysicalBytes) switch
		{
			({ } c, { } s) => Math.Min(c, s),
			({ } c, null)  => c,
			(null, { } s)  => s,
			var _          => null
		};

		// Effective usage = container usage if available, otherwise process working set.
		// This is what you'd compare against effectiveLimit for capacity planning.
		long effectiveUsage = containerUsage ?? workingSetBytes;

		// ---------------------------------------------------------------------
		// Compose DTO
		// ---------------------------------------------------------------------
		var managed = new ManagedHeapMetrics(
			LiveBytes: managedLiveBytes,
			HeapSizeBytes: gc.HeapSizeBytes,
			FragmentedBytes: gc.FragmentedBytes,
			PinnedObjectsCount: gc.PinnedObjectsCount,
			TotalAvailableBytes: gc.TotalAvailableMemoryBytes);

		var processMetrics = new ProcessMemoryMetrics(
			WorkingSetBytes: workingSetBytes,
			PrivateMemoryBytes: privateBytes);

		var system = new SystemMemoryMetrics(
			TotalPhysicalBytes: systemMemory?.TotalPhysicalBytes,
			AvailablePhysicalBytes: systemMemory?.AvailablePhysicalBytes);

		ContainerMetrics? container = (containerLimit, containerUsage) switch
		{
			({ } limit, { } usage) => new ContainerMetrics(LimitBytes: limit, UsageBytes: usage),
			var _                  => null
		};

		var effective = new EffectiveMetrics(
			LimitBytes: effectiveLimit,
			UsageBytes: effectiveUsage);

		return new MemoryMetrics(
			Managed: managed,
			Process: processMetrics,
			System: system,
			Container: container,
			Effective: effective);
	}

	/// <summary>
	/// Attempts to read the container memory limit from Linux cgroup files.
	/// </summary>
	/// <returns>
	/// The container memory limit in bytes if running in a container with a memory constraint;
	/// otherwise, <see langword="null"/>.
	/// </returns>
	/// <remarks>
	///     <para>
	///     Supports both cgroups v2 (newer systems, <c>/sys/fs/cgroup/memory.max</c>) and
	///     cgroups v1 (older systems, <c>/sys/fs/cgroup/memory/memory.limit_in_bytes</c>).
	///     Returns <see langword="null"/> if no limit is set (value is <c>max</c> or near <see cref="long.MaxValue"/>).
	///     </para>
	///     <para>
	///     <b>Limitation:</b> On systems without cgroup namespaces or with a virtual root, the hardcoded paths
	///     may return the host's root cgroup values instead of the container's. A more robust implementation
	///     would parse <c>/proc/self/cgroup</c> to determine the actual cgroup path. This is acceptable for
	///     typical containerized deployments but may report incorrect values in unusual configurations.
	///     </para>
	/// </remarks>
	private static long? TryGetContainerMemoryLimit()
	{
		// Only Linux containers use cgroups.
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			return null;

		// cgroup v2 (unified hierarchy, newer systems like Ubuntu 22.04+)
		const string CgroupV2Path = "/sys/fs/cgroup/memory.max";
		if (File.Exists(CgroupV2Path))
		{
			try
			{
				string content = File.ReadAllText(CgroupV2Path).Trim();

				// "max" means no limit is set.
				if (content == "max")
					return null;

				if (long.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out long bytes))
					return bytes;
			}
			catch (IOException)
			{
				// File system issue.
			}
			catch (UnauthorizedAccessException)
			{
				// Permission denied (restricted container).
			}
		}

		// cgroup v1 (legacy hierarchy, older systems)
		const string CgroupV1Path = "/sys/fs/cgroup/memory/memory.limit_in_bytes";
		if (File.Exists(CgroupV1Path))
		{
			try
			{
				string content = File.ReadAllText(CgroupV1Path).Trim();
				if (long.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out long bytes))
				{
					// Very large values (close to long.MaxValue) indicate "no limit".
					// Typically 9223372036854771712 (0x7FFFFFFFFFFFF000) on 64-bit systems.
					const long NoLimitThreshold = 9_000_000_000_000_000_000L;
					if (bytes < NoLimitThreshold)
						return bytes;
				}
			}
			catch (IOException)
			{
				// File system issue.
			}
			catch (UnauthorizedAccessException)
			{
				// Permission denied (restricted container).
			}
		}

		return null;
	}

	/// <summary>
	/// Attempts to read the current container memory usage from Linux cgroup files.
	/// </summary>
	/// <returns>
	/// The current container memory usage in bytes if running in a container;
	/// otherwise, <see langword="null"/>.
	/// </returns>
	/// <remarks>
	///     <para>
	///     This is the value the OOM killer uses to decide when to terminate the container.
	///     Includes all memory charged to the cgroup: process RSS, page cache, kernel structures, etc.
	///     </para>
	///     <para>
	///     See <see cref="TryGetContainerMemoryLimit"/> remarks for known limitations regarding cgroup path detection.
	///     </para>
	/// </remarks>
	private static long? TryGetContainerMemoryUsage()
	{
		// Only Linux containers use cgroups.
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			return null;

		// cgroup v2 (unified hierarchy, newer systems like Ubuntu 22.04+)
		const string CgroupV2Path = "/sys/fs/cgroup/memory.current";
		if (File.Exists(CgroupV2Path))
		{
			try
			{
				string content = File.ReadAllText(CgroupV2Path).Trim();
				if (long.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out long bytes))
					return bytes;
			}
			catch (IOException)
			{
				// File system issue.
			}
			catch (UnauthorizedAccessException)
			{
				// Permission denied.
			}
		}

		// cgroup v1 (legacy hierarchy, older systems)
		const string CgroupV1Path = "/sys/fs/cgroup/memory/memory.usage_in_bytes";
		if (File.Exists(CgroupV1Path))
		{
			try
			{
				string content = File.ReadAllText(CgroupV1Path).Trim();
				if (long.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out long bytes))
					return bytes;
			}
			catch (IOException)
			{
				// File system issue.
			}
			catch (UnauthorizedAccessException)
			{
				// Permission denied.
			}
		}

		return null;
	}

	/// <summary>
	/// Attempts to retrieve system-level physical memory information using OS-specific mechanisms.
	/// </summary>
	/// <returns>
	/// A <see cref="SystemMemoryInfo"/> instance if supported on the current OS;
	/// otherwise, <see langword="null"/>.
	/// </returns>
	private static SystemMemoryInfo? TryGetSystemMemory()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return TryGetWindowsMemory();

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			return TryGetLinuxMemory();

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			return TryGetMacOSMemory();

		return null;
	}

	/// <summary>
	/// Retrieves system memory information on Linux systems by parsing <c>/proc/meminfo</c>.
	/// </summary>
	/// <returns>
	/// A <see cref="SystemMemoryInfo"/> with total and available physical memory,
	/// or <see langword="null"/> if the file cannot be read or parsed.
	/// </returns>
	private static SystemMemoryInfo? TryGetLinuxMemory()
	{
		const string MemInfoPath = "/proc/meminfo";
		if (!File.Exists(MemInfoPath))
			return null;

		long? totalBytes = null;
		long? availableBytes = null;

		// /proc/meminfo is a simple key-value text file.
		// We read MemAvailable (kernel estimate including reclaimable caches),
		// not MemFree (just free pages). MemAvailable exists since Linux 3.14 (2014).
		foreach (string line in File.ReadLines(MemInfoPath))
		{
			// Examples:
			// "MemTotal:       32725352 kB"
			// "MemAvailable:   12345678 kB"
			if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
				totalBytes = ParseMemInfoKbToBytes(line);
			else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
				availableBytes = ParseMemInfoKbToBytes(line);

			if (totalBytes.HasValue && availableBytes.HasValue)
				break;
		}

		if (!totalBytes.HasValue || !availableBytes.HasValue)
			return null;

		return new SystemMemoryInfo(
			TotalPhysicalBytes: totalBytes.Value,
			AvailablePhysicalBytes: availableBytes.Value);
	}

	/// <summary>
	/// Parses a single line from <c>/proc/meminfo</c> and converts the reported kilobyte value to bytes.
	/// </summary>
	/// <param name="line">A line from <c>/proc/meminfo</c> in the format <c>"Key: value kB"</c>.</param>
	/// <returns>The value in bytes, or <see langword="null"/> if parsing fails.</returns>
	private static long? ParseMemInfoKbToBytes(string line)
	{
		// Expected format: "<Key>: <value> kB"
		string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2)
			return null;

		if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long kb))
			return null;

		return kb * 1024L;
	}

	/// <summary>
	/// Retrieves system memory information on Windows systems using the native <c>GlobalMemoryStatusEx</c> API.
	/// </summary>
	/// <returns>
	/// A <see cref="SystemMemoryInfo"/> with total and available physical memory,
	/// or <see langword="null"/> if the API call fails.
	/// </returns>
	private static SystemMemoryInfo? TryGetWindowsMemory()
	{
		var status = new MEMORYSTATUSEX
		{
			dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
		};

		if (!GlobalMemoryStatusEx(ref status))
			return null;

		return new SystemMemoryInfo(
			TotalPhysicalBytes: unchecked((long)status.ullTotalPhys),
			AvailablePhysicalBytes: unchecked((long)status.ullAvailPhys));
	}

	/// <summary>
	/// Retrieves system memory information on macOS using native <c>sysctl</c> and <c>host_statistics64</c> APIs.
	/// </summary>
	/// <returns>
	/// A <see cref="SystemMemoryInfo"/> with total and available physical memory, or <see langword="null"/> if
	/// the API calls fail.
	/// </returns>
	private static SystemMemoryInfo? TryGetMacOSMemory()
	{
		// Get total physical memory via sysctl("hw.memsize")
		nint size = sizeof(long);
		if (sysctlbyname("hw.memsize", out long totalBytes, ref size, IntPtr.Zero, 0) != 0)
			return null;

		// Get memory page size for vm_statistics64 calculations
		nint pageSizeLen = sizeof(int);
		if (sysctlbyname("hw.pagesize", out int pageSize, ref pageSizeLen, IntPtr.Zero, 0) != 0)
			return null;

		// Get available memory via host_statistics64
		uint host = mach_host_self();
		var vmStats = new vm_statistics64_data();
		uint count = (uint)(Marshal.SizeOf<vm_statistics64_data>() / sizeof(int));

		if (host_statistics64(host, HostStatisticsFlavor.VmInfo, ref vmStats, ref count) != 0)
			return null;

		// Available = free + inactive (pages that can be reclaimed without I/O).
		// This is an approximation — macOS doesn't expose a single "available" metric.
		// Activity Monitor uses "Memory Pressure" which is more complex.
		// For capacity planning, this conservative estimate is appropriate.
		long availableBytes = (vmStats.free_count + (long)vmStats.inactive_count) * pageSize;

		return new SystemMemoryInfo(
			TotalPhysicalBytes: totalBytes,
			AvailablePhysicalBytes: availableBytes);
	}

	/// <summary>
	/// Internal value type representing host-level physical memory values.
	/// </summary>
	/// <param name="TotalPhysicalBytes">Total physical RAM on the host machine.</param>
	/// <param name="AvailablePhysicalBytes">Currently available physical RAM on the host machine.</param>
	private readonly record struct SystemMemoryInfo(
		long TotalPhysicalBytes,
		long AvailablePhysicalBytes);

	#region Windows native interop

	/// <summary>
	/// Contains information about the current state of both physical and virtual memory on Windows.
	/// </summary>
	/// <remarks>
	/// See <see href="https://learn.microsoft.com/windows/win32/api/sysinfoapi/ns-sysinfoapi-memorystatusex"/>.
	/// </remarks>
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private struct MEMORYSTATUSEX
	{
		/// <summary>Size of this structure in bytes. Must be set before calling <see cref="GlobalMemoryStatusEx"/>.</summary>
		public uint dwLength;

		/// <summary>Approximate percentage of physical memory currently in use (0-100).</summary>
		public uint dwMemoryLoad;

		/// <summary>Total size of physical memory (RAM) in bytes.</summary>
		public ulong ullTotalPhys;

		/// <summary>Available physical memory in bytes.</summary>
		public ulong ullAvailPhys;

		/// <summary>Total size of the paging file (swap) in bytes.</summary>
		public ulong ullTotalPageFile;

		/// <summary>Available space in the paging file in bytes.</summary>
		public ulong ullAvailPageFile;

		/// <summary>Total size of the user-mode virtual address space in bytes.</summary>
		public ulong ullTotalVirtual;

		/// <summary>Available user-mode virtual address space in bytes.</summary>
		public ulong ullAvailVirtual;

		/// <summary>Reserved. Always 0.</summary>
		public ulong ullAvailExtendedVirtual;
	}

	/// <summary>
	/// Retrieves information about the system's current usage of both physical and virtual memory.
	/// </summary>
	/// <param name="lpBuffer">
	/// A reference to a <see cref="MEMORYSTATUSEX"/> structure. Set <see cref="MEMORYSTATUSEX.dwLength"/> before calling.
	/// </param>
	/// <returns><see langword="true"/> if the function succeeds; otherwise, <see langword="false"/>.</returns>
	/// <remarks>
	/// See <see href="https://learn.microsoft.com/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex"/>.
	/// </remarks>
	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

	#endregion

	#region macOS native interop

	/// <summary>
	/// Retrieves system information by name on macOS/BSD systems.
	/// </summary>
	/// <param name="name">The system variable name (e.g., <c>"hw.memsize"</c>).</param>
	/// <param name="oldp">Buffer to receive the value.</param>
	/// <param name="oldlenp">Size of the buffer; updated with actual size on return.</param>
	/// <param name="newp">New value to set (use <see cref="IntPtr.Zero"/> for read-only).</param>
	/// <param name="newlen">Size of new value.</param>
	/// <returns>0 on success; -1 on error.</returns>
	/// <remarks>See the BSD <c>sysctlbyname(3)</c> man page for details.</remarks>
	[DllImport("libSystem.dylib", CallingConvention = CallingConvention.Cdecl)]
	private static extern int sysctlbyname(
		[MarshalAs(UnmanagedType.LPStr)] string name,
		out                              long   oldp,
		ref                              nint   oldlenp,
		IntPtr                                  newp,
		nint                                    newlen);

	/// <summary>
	/// Retrieves system information by name on macOS/BSD systems (int overload for page size).
	/// </summary>
	[DllImport("libSystem.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sysctlbyname")]
	private static extern int sysctlbyname(
		[MarshalAs(UnmanagedType.LPStr)] string name,
		out                              int    oldp,
		ref                              nint   oldlenp,
		IntPtr                                  newp,
		nint                                    newlen);

	/// <summary>
	/// Returns the host port for the current task.
	/// </summary>
	/// <returns>The host port.</returns>
	/// <remarks>
	/// See <see href="https://developer.apple.com/documentation/kernel/1502514-mach_host_self"/>.
	/// </remarks>
	[DllImport("libSystem.dylib", CallingConvention = CallingConvention.Cdecl)]
	private static extern uint mach_host_self();

	/// <summary>
	/// Returns 64-bit statistics for a host.
	/// </summary>
	/// <param name="host_priv">The host port from <see cref="mach_host_self"/>.</param>
	/// <param name="flavor">The type of statistics to retrieve.</param>
	/// <param name="host_info_out">Buffer to receive the statistics.</param>
	/// <param name="host_info_outCnt">Size of buffer in integers; updated on return.</param>
	/// <returns>0 (<c>KERN_SUCCESS</c>) on success; non-zero on error.</returns>
	/// <remarks>
	/// See <see href="https://developer.apple.com/documentation/kernel/1502863-host_statistics64"/>.
	/// </remarks>
	[DllImport("libSystem.dylib", CallingConvention = CallingConvention.Cdecl)]
	private static extern int host_statistics64(
		uint                     host_priv,
		HostStatisticsFlavor     flavor,
		ref vm_statistics64_data host_info_out,
		ref uint                 host_info_outCnt);

	/// <summary>
	/// Flavor constants for <see cref="host_statistics64"/>.
	/// </summary>
	private enum HostStatisticsFlavor
	{
		/// <summary>VM statistics (<see cref="vm_statistics64_data"/>).</summary>
		VmInfo = 4
	}

	/// <summary>
	/// Virtual memory statistics structure for macOS (64-bit version).
	/// </summary>
	/// <remarks>
	/// Layout must match the XNU kernel's <c>vm_statistics64</c> struct exactly.
	/// See <see href="https://opensource.apple.com/source/xnu/xnu-7195.81.3/osfmk/mach/vm_statistics.h"/>.
	/// </remarks>
	[StructLayout(LayoutKind.Sequential)]
	private struct vm_statistics64_data
	{
		/// <summary>Pages not in use (immediately available).</summary>
		public uint free_count;

		/// <summary>Pages in use by active processes.</summary>
		public uint active_count;

		/// <summary>Pages not recently used (candidates for reclamation).</summary>
		public uint inactive_count;

		/// <summary>Pages wired down (cannot be paged out).</summary>
		public uint wire_count;

		/// <summary>Zero-filled pages.</summary>
		public ulong zero_fill_count;

		/// <summary>Page reactivations.</summary>
		public ulong reactivations;

		/// <summary>Pages paged in.</summary>
		public ulong pageins;

		/// <summary>Pages paged out.</summary>
		public ulong pageouts;

		/// <summary>Page faults.</summary>
		public ulong faults;

		/// <summary>Copy-on-write faults.</summary>
		public ulong cow_faults;

		/// <summary>Object lookups.</summary>
		public ulong lookups;

		/// <summary>Object hits.</summary>
		public ulong hits;

		/// <summary>Pages purged.</summary>
		public ulong purges;

		/// <summary>Purgeable pages count.</summary>
		public uint purgeable_count;

		/// <summary>Speculative pages count.</summary>
		public uint speculative_count;

		/// <summary>Pages decompressed.</summary>
		public ulong decompressions;

		/// <summary>Pages compressed.</summary>
		public ulong compressions;

		/// <summary>Pages swapped in.</summary>
		public ulong swapins;

		/// <summary>Pages swapped out.</summary>
		public ulong swapouts;

		/// <summary>Compressed pages in compressor.</summary>
		public uint compressor_page_count;

		/// <summary>Throttled pages.</summary>
		public uint throttled_count;

		/// <summary>External pages (file-backed).</summary>
		public uint external_page_count;

		/// <summary>Internal pages (anonymous).</summary>
		public uint internal_page_count;

		/// <summary>Total uncompressed pages in compressor.</summary>
		public ulong total_uncompressed_pages_in_compressor;
	}

	#endregion
}

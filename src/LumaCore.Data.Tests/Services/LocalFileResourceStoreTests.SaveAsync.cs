// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Services;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LocalFileResourceStoreTests
{
	#region SaveAsync

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.SaveAsync"/> writes the stream content to disk under
	/// the storage root.
	/// </summary>
	[Fact]
	public async Task SaveAsync_WhenStoragePathIsValid_WritesFileWithContent()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		await using MemoryStream content = MakeStream("hello, world");

		// Act
		await sut.SaveAsync("file.bin", content);

		// Assert
		string written = await File.ReadAllTextAsync(Path.Combine(root.Path, "file.bin"));
		Assert.Equal("hello, world", written);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.SaveAsync"/> creates intermediate directories
	/// automatically (supports future shard-prefix layouts like <c>"a1/file"</c>).
	/// </summary>
	[Fact]
	public async Task SaveAsync_WhenParentDirectoryMissing_CreatesIntermediateDirectories()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		await using MemoryStream content = MakeStream("sharded");

		// Act
		await sut.SaveAsync("a1/sub/file.bin", content);

		// Assert
		Assert.True(File.Exists(Path.Combine(root.Path, "a1", "sub", "file.bin")));
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.SaveAsync"/> refuses to overwrite an existing file
	/// (uses <see cref="FileMode.CreateNew"/> to guard against silent data loss on GUID collisions).
	/// </summary>
	[Fact]
	public async Task SaveAsync_WhenFileAlreadyExists_ThrowsIOException()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		await using MemoryStream first = MakeStream("first");
		await sut.SaveAsync("existing.bin", first);

		// Act + Assert
		await using MemoryStream second = MakeStream("second");
		await Assert.ThrowsAsync<IOException>(() => sut.SaveAsync("existing.bin", second));

		// Verify the original content survived.
		string preserved = await File.ReadAllTextAsync(Path.Combine(root.Path, "existing.bin"));
		Assert.Equal("first", preserved);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.SaveAsync"/> deletes the partially written file when
	/// the source stream throws mid-copy. Without this cleanup the orphan would block any retry because
	/// <see cref="FileMode.CreateNew"/> refuses to overwrite.
	/// </summary>
	[Fact]
	public async Task SaveAsync_WhenStreamThrowsMidCopy_DeletesPartialFileAndRethrows()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		await using var failing = new ThrowingStream(throwAfter: 8);

		// Act + Assert — the original IOException must propagate (verifying the message ensures the
		// finally-block cleanup didn't replace it with a delete-side IOException of its own).
		var ex = await Assert.ThrowsAsync<IOException>(() => sut.SaveAsync("partial.bin", failing));
		Assert.Equal("simulated mid-copy failure", ex.Message);

		// The partial file must have been removed so a retry with the same path is possible.
		Assert.False(File.Exists(Path.Combine(root.Path, "partial.bin")));
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.SaveAsync"/> propagates cancellation and removes the
	/// partial file the same way it does for IO errors.
	/// </summary>
	[Fact]
	public async Task SaveAsync_WhenCancelled_DeletesPartialFileAndThrowsOperationCanceled()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		await using MemoryStream content = MakeStream("never written");

		// Act + Assert — accept any OperationCanceledException subtype so the test stays robust
		// against future BCL changes (TaskCanceledException is a subclass).
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			sut.SaveAsync("cancelled.bin", content, cts.Token));

		Assert.False(File.Exists(Path.Combine(root.Path, "cancelled.bin")));
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.SaveAsync"/> rejects whitespace-only storage paths.
	/// </summary>
	[Fact]
	public async Task SaveAsync_WhenStoragePathIsWhitespace_ThrowsArgumentException()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		await using MemoryStream content = MakeStream("x");

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.SaveAsync("   ", content));
		Assert.Equal("storagePath", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.SaveAsync"/> rejects a <see langword="null"/> content
	/// stream.
	/// </summary>
	[Fact]
	public async Task SaveAsync_WhenContentIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);

		// Act + Assert
		var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SaveAsync("file.bin", null!));
		Assert.Equal("content", ex.ParamName);
	}

	/// <summary>
	/// Verifies that <see cref="LocalFileResourceStore.SaveAsync"/> rejects a path-traversal attempt and
	/// does not write any file.
	/// </summary>
	/// <param name="escapingPath">The malicious storage path that escapes the storage root.</param>
	[Theory]
	[MemberData(nameof(EscapingPaths))]
	public async Task SaveAsync_WhenPathEscapesRoot_ThrowsInvalidOperationException(string escapingPath)
	{
		// Arrange
		using var root = new TempStorageRoot();
		LocalFileResourceStore sut = CreateStore(root.Path);
		using MemoryStream content = MakeStream("payload");

		// Act
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SaveAsync(
			         escapingPath,
			         content,
			         CancellationToken.None));

		// Assert: exact message including the absolute path the SUT computed (mirrors the production
		// formatter so a future refactor cannot silently weaken the diagnostic).
		string expectedAbsolute = Path.GetFullPath(
			Path.Combine(
				Path.GetFullPath(root.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				+ Path.DirectorySeparatorChar,
				escapingPath));
		Assert.Equal(
			$"Path traversal detected: resolved path '{expectedAbsolute}' escapes storage root.",
			ex.Message);
		// Negative expectation: nothing must have been written outside (or inside) the storage root.
		Assert.Empty(Directory.EnumerateFileSystemEntries(root.Path));
	}

	#endregion

	/// <summary>
	/// Stream that throws an <see cref="IOException"/> after the configured number of bytes have been read.
	/// Used to simulate a partial-write failure in <c>SaveAsync</c>.
	/// </summary>
	private sealed class ThrowingStream : Stream
	{
		private readonly int mThrowAfter;
		private          int mProduced;

		/// <summary>
		/// Initializes a new <see cref="ThrowingStream"/> that throws after <paramref name="throwAfter"/> bytes.
		/// </summary>
		/// <param name="throwAfter">The number of bytes to deliver before throwing.</param>
		public ThrowingStream(int throwAfter)
		{
			mThrowAfter = throwAfter;
		}

		public override bool CanRead  => true;
		public override bool CanSeek  => false;
		public override bool CanWrite => false;
		public override long Length   => mThrowAfter * 2;

		public override long Position
		{
			get => mProduced;
			set => throw new NotSupportedException();
		}

		public override void Flush() { }

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (mProduced >= mThrowAfter)
				throw new IOException("simulated mid-copy failure");

			int toReturn = Math.Min(count, mThrowAfter - mProduced);
			for (int i = 0; i < toReturn; i++)
			{
				buffer[offset + i] = (byte)'x';
			}

			mProduced += toReturn;
			return toReturn;
		}

		public override long Seek(long      offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value)                         => throw new NotSupportedException();
		public override void Write(byte[]   buffer, int offset, int count) => throw new NotSupportedException();
	}
}

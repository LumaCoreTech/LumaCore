// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

// ==============================================================================
// LumaCore.HealthCheck
// ==============================================================================
// A minimal health check client designed for Docker HEALTHCHECK instructions.
//
// This tool avoids the need to install curl or wget in container images,
// reducing image size and attack surface while using the same .NET runtime
// as the main application.
//
// Usage:
//   dotnet LumaCore.HealthCheck.dll [url] [timeout_seconds]
//
// Environment Variables:
//   HEALTHCHECK_URL              Target URL (default: http://localhost:5080/api/v1/health/live)
//   HEALTHCHECK_TIMEOUT_SECONDS  Request timeout (default: 5)
//
// Exit Codes:
//   0  Health check passed (HTTP 2xx response)
//   1  Health check failed (non-2xx response, timeout, or connection error)
//
// Examples:
//   dotnet LumaCore.HealthCheck.dll
//   dotnet LumaCore.HealthCheck.dll http://localhost:8080/api/v1/health/live
//   dotnet LumaCore.HealthCheck.dll http://localhost:8080/api/v1/health/live 10
//
// Docker Usage:
//   HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
//       CMD ["dotnet", "/app/healthcheck/LumaCore.HealthCheck.dll"]
// ==============================================================================

return await HealthCheckRunner.ExecuteAsync(args).ConfigureAwait(false);

/// <summary>
/// Executes HTTP-based health checks and returns appropriate exit codes for Docker.
/// </summary>
/// <remarks>
///     <para>
///     This static class provides the core health check logic used by the entry point.
///     It is designed to be minimal and dependency-free, relying only on the base
///     class library's <see cref="HttpClient"/>.
///     </para>
///     <para>
///     The health check considers any HTTP 2xx response as healthy. All other responses,
///     as well as connection failures and timeouts, are considered unhealthy.
///     </para>
/// </remarks>
static class HealthCheckRunner
{
	/// <summary>
	/// The default URL to check when no URL is provided via arguments or environment.
	/// </summary>
	/// <value>
	/// <c>http://localhost:5080/api/v1/health/live</c> — the LumaCore API liveness endpoint.
	/// </value>
	private const string DefaultUrl = "http://localhost:5080/api/v1/health/live";

	/// <summary>
	/// The default timeout in seconds for health check requests.
	/// </summary>
	/// <value>
	/// 5 seconds — sufficient for a simple liveness check while failing fast
	/// enough to allow Docker to detect unresponsive containers.
	/// </value>
	private const int DefaultTimeoutSeconds = 5;

	/// <summary>
	/// Environment variable name for configuring the health check URL.
	/// </summary>
	private const string EnvVarUrl = "HEALTHCHECK_URL";

	/// <summary>
	/// Environment variable name for configuring the request timeout.
	/// </summary>
	private const string EnvVarTimeout = "HEALTHCHECK_TIMEOUT_SECONDS";

	/// <summary>
	/// Exit code indicating a successful health check.
	/// </summary>
	private const int ExitCodeHealthy = 0;

	/// <summary>
	/// Exit code indicating a failed health check.
	/// </summary>
	private const int ExitCodeUnhealthy = 1;

	/// <summary>
	/// Executes the health check against the configured endpoint.
	/// </summary>
	/// <param name="args">
	/// Command-line arguments. The first argument (if present) overrides the URL,
	/// and the second argument (if present) overrides the timeout in seconds.
	/// </param>
	/// <returns>
	/// A task that resolves to <see cref="ExitCodeHealthy"/> (<c>0</c>) if the health
	/// check passed, or <see cref="ExitCodeUnhealthy"/> (<c>1</c>) if it failed.
	/// </returns>
	/// <remarks>
	///     <para>
	///     Configuration is resolved in the following priority order:
	///     </para>
	///     <list type="number">
	///         <item>
	///             <description>Command-line arguments</description>
	///         </item>
	///         <item>
	///             <description>Environment variables</description>
	///         </item>
	///         <item>
	///             <description>Default values</description>
	///         </item>
	///     </list>
	///     <para>
	///     This method never throws exceptions. All errors are caught and result in
	///     an unhealthy exit code, as expected by Docker's HEALTHCHECK instruction.
	///     </para>
	/// </remarks>
	public static async Task<int> ExecuteAsync(string[] args)
	{
		string url = ResolveUrl(args);
		int timeoutSeconds = ResolveTimeout(args);

		try
		{
			using HttpClient httpClient = CreateHttpClient(timeoutSeconds);
			using HttpResponseMessage response = await httpClient
				                                     .GetAsync(url)
				                                     .ConfigureAwait(false);

			if (response.IsSuccessStatusCode)
			{
				WriteOutput($"Healthy: {url} returned {(int)response.StatusCode} {response.ReasonPhrase}");
				return ExitCodeHealthy;
			}

			WriteOutput($"Unhealthy: {url} returned {(int)response.StatusCode} {response.ReasonPhrase}");
			return ExitCodeUnhealthy;
		}
		catch (TaskCanceledException)
		{
			// HttpClient throws TaskCanceledException when the timeout is exceeded.
			WriteOutput($"Unhealthy: Request to {url} timed out after {timeoutSeconds} seconds");
			return ExitCodeUnhealthy;
		}
		catch (HttpRequestException ex)
		{
			WriteOutput($"Unhealthy: Connection to {url} failed — {ex.Message}");
			return ExitCodeUnhealthy;
		}
		catch (Exception ex)
		{
			// Catch-all for unexpected errors (e.g., invalid URL format).
			WriteOutput($"Unhealthy: Unexpected error checking {url} — {ex.Message}");
			return ExitCodeUnhealthy;
		}
	}

	/// <summary>
	/// Resolves the target URL from arguments, environment, or defaults.
	/// </summary>
	/// <param name="args">Command-line arguments.</param>
	/// <returns>The URL to check.</returns>
	private static string ResolveUrl(string[] args)
	{
		// Priority 1: Command-line argument
		if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
		{
			return args[0];
		}

		// Priority 2: Environment variable
		string? envUrl = Environment.GetEnvironmentVariable(EnvVarUrl);
		if (!string.IsNullOrWhiteSpace(envUrl))
		{
			return envUrl;
		}

		// Priority 3: Default
		return DefaultUrl;
	}

	/// <summary>
	/// Resolves the request timeout from arguments, environment, or defaults.
	/// </summary>
	/// <param name="args">Command-line arguments.</param>
	/// <returns>The timeout in seconds.</returns>
	private static int ResolveTimeout(string[] args)
	{
		// Priority 1: Command-line argument (second argument)
		if (args.Length > 1 && int.TryParse(args[1], out int argTimeout) && argTimeout > 0)
		{
			return argTimeout;
		}

		// Priority 2: Environment variable
		string? envTimeout = Environment.GetEnvironmentVariable(EnvVarTimeout);
		if (!string.IsNullOrWhiteSpace(envTimeout) &&
		    int.TryParse(envTimeout, out int parsedEnvTimeout) &&
		    parsedEnvTimeout > 0)
		{
			return parsedEnvTimeout;
		}

		// Priority 3: Default
		return DefaultTimeoutSeconds;
	}

	/// <summary>
	/// Creates an <see cref="HttpClient"/> configured for health check requests.
	/// </summary>
	/// <param name="timeoutSeconds">The request timeout in seconds.</param>
	/// <returns>A new <see cref="HttpClient"/> instance.</returns>
	/// <remarks>
	/// The client is configured with a short timeout suitable for health checks.
	/// The caller is responsible for disposing the returned instance.
	/// </remarks>
	private static HttpClient CreateHttpClient(int timeoutSeconds)
	{
		return new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(timeoutSeconds)
		};
	}

	/// <summary>
	/// Writes a message to standard output.
	/// </summary>
	/// <param name="message">The message to write.</param>
	/// <remarks>
	/// Output is written to stdout so it can be captured by <c>docker inspect</c>
	/// in the health check log. This aids debugging when containers are unhealthy.
	/// </remarks>
	private static void WriteOutput(string message)
	{
		Console.WriteLine($"[LumaCore.HealthCheck] {message}");
	}
}

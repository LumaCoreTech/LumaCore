// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;

using LumaCore.Api.Features.ApiVersioning;
using LumaCore.Api.Features.Auth;
using LumaCore.Api.Features.ErrorHandling;
using LumaCore.Api.Features.UserManagement;
using LumaCore.Data;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

using Xunit;

using V1 = LumaCore.Api.Contracts.V1.Auth;

namespace LumaCore.Api.Tests.Features.Auth;

public sealed partial class AuthIntegrationTests
{
	/// <summary>
	/// The signing key used across all integration tests. Must satisfy the
	/// <see cref="JwtOptions.SigningKey"/> minimum-length requirement (32 characters).
	/// </summary>
	private const string TestSigningKey = "a-test-signing-key-that-is-at-least-32-characters-long!!!";

	/// <summary>
	/// The username used for login across all integration tests. Also used as the expected <c>sub</c> and
	/// <see cref="ClaimTypes.Name"/> claim value.
	/// </summary>
	private const string TestUsername = "admin";

	/// <summary>
	/// The password used for login across all integration tests.
	/// </summary>
	private const string TestPassword = "changeme";

	/// <summary>
	/// The expected <see cref="ClaimTypes.Role"/> claim value for the seeded test account.
	/// </summary>
	private const string TestRole = "admin";

	/// <summary>
	/// The JWT issuer configured in the test harness.
	/// </summary>
	private const string TestIssuer = "test-issuer";

	/// <summary>
	/// The JWT audience configured in the test harness.
	/// </summary>
	private const string TestAudience = "test-audience";

	/// <summary>
	/// The access token lifetime in minutes configured in the test harness.
	/// </summary>
	private const int TestAccessTokenLifetimeMinutes = 60;

	/// <summary>
	/// Encapsulates all test infrastructure for auth integration tests: a <see cref="TestServer"/>-backed
	/// <see cref="WebApplication"/> with JWT authentication, API versioning, SQLite in-memory database,
	/// and the full auth endpoint mapping.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The SQLite in-memory connection is kept open for the lifetime of the harness. Disposing the
	///     harness stops the application, disposes the <see cref="HttpClient"/>, and closes the
	///     connection (which destroys the in-memory database).
	///     </para>
	///     <para>
	///     Per project conventions, do not use <c>await using</c> — use <c>try/finally</c> with explicit
	///     <see cref="DisposeAsync"/> instead.
	///     </para>
	/// </remarks>
	private sealed class TestHarness : IAsyncDisposable
	{
		private readonly SqliteConnection mConnection;
		private readonly WebApplication   mApp;

		/// <summary>
		/// Gets the <see cref="HttpClient"/> connected to the in-memory <see cref="TestServer"/>.
		/// </summary>
		public HttpClient Client { get; }

		/// <summary>
		/// Gets the fake time provider for controlling token timestamps and expiry in tests.
		/// </summary>
		public FakeTimeProvider TimeProvider { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TestHarness"/> class. Use <see cref="CreateAsync"/>
		/// to build an instance.
		/// </summary>
		private TestHarness(
			SqliteConnection connection,
			WebApplication   app,
			HttpClient       client,
			FakeTimeProvider timeProvider)
		{
			mConnection = connection;
			mApp = app;
			Client = client;
			TimeProvider = timeProvider;
		}

		/// <summary>
		/// Builds a fully configured test harness with JWT authentication, API versioning,
		/// SQLite in-memory database, and the auth endpoint mapping.
		/// </summary>
		/// <param name="cookieEnabled">
		/// When <see langword="true"/>, enables cookie transport so the login endpoint sets an
		/// <c>HttpOnly</c> cookie alongside the JSON response.
		/// </param>
		/// <param name="cookieName">
		/// The cookie name used when <paramref name="cookieEnabled"/> is <see langword="true"/>.
		/// </param>
		/// <returns>A disposable harness ready for HTTP requests.</returns>
		public static async Task<TestHarness> CreateAsync(
			bool   cookieEnabled = false,
			string cookieName    = "lumacore-token")
		{
			// SQLite in-memory connection — kept open for the lifetime of the harness.
			var connection = new SqliteConnection("Data Source=:memory:");
			await connection.OpenAsync().ConfigureAwait(false);

			var config = new Dictionary<string, string?>
			{
				["Jwt:Issuer"] = TestIssuer,
				["Jwt:Audience"] = TestAudience,
				["Jwt:SigningKey"] = TestSigningKey,
				["Jwt:AccessTokenLifetimeMinutes"] =
					TestAccessTokenLifetimeMinutes.ToString(CultureInfo.InvariantCulture),
				["Jwt:Cookie:Enabled"] = cookieEnabled.ToString(),
				["Jwt:Cookie:Name"] = cookieName,
				["Jwt:Cookie:Path"] = "/api",
				["Jwt:TokenRevocation:CacheDurationSeconds"] = "0"
			};

			// Use Production environment to avoid ValidateOnBuild/ValidateScopes issues
			// that some test runners trigger by injecting ASPNETCORE_ENVIRONMENT=Development.
			WebApplicationBuilder builder = WebApplication.CreateBuilder(
				new WebApplicationOptions { EnvironmentName = Environments.Production });

			builder.WebHost.UseTestServer();
			builder.Configuration.AddInMemoryCollection(config);

			// Core services required by the auth pipeline.
			builder.Services.AddProblemDetails();
			builder.Services.AddApiVersioningFeatureCore();
			builder.Services.AddAuthFeatureCore(builder.Configuration);

			// In-memory user authentication for test isolation — completely decoupled from
			// production credentials. The test harness seeds its own users via the constants
			// at the top of this class (TestUsername, TestPassword, TestRole).
			var userService = new InMemoryUserAuthenticationService();
			userService.AddUser(TestUsername, TestPassword, TestRole);
			builder.Services.AddSingleton<IUserAuthenticationService>(userService);

			// FakeTimeProvider makes token timestamps deterministic and enables future expiry tests
			// via Advance(). Same starting point as TokenRevocationServiceTests.TestHarness.
			var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
			builder.Services.AddSingleton<TimeProvider>(timeProvider);

			// Override the default lifetime validation so it uses our FakeTimeProvider instead of
			// DateTime.UtcNow. The Microsoft.IdentityModel token handler does not consult ASP.NET
			// Core's TimeProvider for nbf/exp checks, so a custom LifetimeValidator is required.
			builder.Services
				.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
				.PostConfigure(options =>
				{
					options.TokenValidationParameters.LifetimeValidator =
						(
							notBefore,
							expires,
							_,
							parameters) =>
						{
							DateTime now = timeProvider.GetUtcNow().UtcDateTime;
							TimeSpan skew = parameters.ClockSkew;

							if (notBefore.HasValue && now + skew < notBefore.Value)
								return false;

							if (expires.HasValue && now - skew > expires.Value)
								return false;

							return true;
						};
				});

			// SQLite in-memory database — shared connection so all scoped DbContext instances
			// see the same data. Same pattern as TokenRevocationServiceTests.TestHarness.
			builder.Services.AddDbContext<LumaCoreDbContext>(options =>
				options
					.UseSqlite(connection)
					.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning)));

			WebApplication app = builder.Build();

			// Create the database schema before the app starts handling requests.
			IServiceScope scope = app.Services.CreateScope();
			try
			{
				var db = scope.ServiceProvider.GetRequiredService<LumaCoreDbContext>();
				await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
			}
			finally
			{
				if (scope is IAsyncDisposable asyncScope)
					await asyncScope.DisposeAsync().ConfigureAwait(false);
				else
					scope.Dispose();
			}

			// Pipeline — mirrors production ordering from Program.Pipeline.cs.
			// UseErrorHandlingFeature() wraps UseStatusCodePages, converting bare error
			// status codes (401, 404, …) into RFC 7807 ProblemDetails with LumaCore URNs.
			app.UseErrorHandlingFeature();
			app.UseRouting();
			app.UseAuthentication();
			app.UseAuthorization();

			RouteGroupBuilder api = app.MapVersionedApiGroup();
			api.MapAuthFeature();

			await app.StartAsync().ConfigureAwait(false);

			return new TestHarness(connection, app, app.GetTestClient(), timeProvider);
		}

		/// <summary>
		/// Stops the application, disposes the <see cref="HttpClient"/>, and closes the SQLite connection.
		/// </summary>
		public async ValueTask DisposeAsync()
		{
			Client.Dispose();
			await mApp.StopAsync().ConfigureAwait(false);
			await mApp.DisposeAsync().ConfigureAwait(false);
			await mConnection.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Parses a serialized JWT and asserts that all claims match the expected values. Verifies header
	/// fields (<c>alg</c>, <c>typ</c>), identity claims (<c>sub</c>, <see cref="ClaimTypes.Name"/>,
	/// <see cref="ClaimTypes.Role"/>), configuration claims (<c>iss</c>, <c>aud</c>), temporal claims
	/// (<c>nbf</c>, <c>exp</c>), the token ID format, and that no unexpected claim types are present.
	/// </summary>
	/// <param name="tokenString">The serialized JWT to parse and verify.</param>
	/// <param name="expectedSubject">
	/// The expected <c>sub</c> claim value. The <see cref="ClaimTypes.Name"/> claim is also verified
	/// to match this value (the login endpoint sets both to the username).
	/// </param>
	/// <param name="expectedRoles">
	/// The expected <see cref="ClaimTypes.Role"/> claim values. Pass an empty array when the token
	/// carries no roles.
	/// </param>
	/// <param name="issuedAtUtc">
	/// The UTC timestamp when the token was issued. Used as the expected <c>nbf</c> value; the expected
	/// <c>exp</c> is computed by adding <see cref="TestAccessTokenLifetimeMinutes"/>.
	/// </param>
	/// <returns>The parsed <see cref="JwtSecurityToken"/> for additional assertions by the caller.</returns>
	private static JwtSecurityToken AssertTokenClaims(
		string   tokenString,
		string   expectedSubject,
		string[] expectedRoles,
		DateTime issuedAtUtc)
	{
		var handler = new JwtSecurityTokenHandler();
		JwtSecurityToken token = handler.ReadJwtToken(tokenString);

		// --- Header ---
		Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
		Assert.Equal("JWT", token.Header.Typ);

		// --- Identity claims ---
		Assert.Equal(expectedSubject, token.Subject);

		// The login endpoint adds ClaimTypes.Name alongside sub — verify it matches.
		Claim nameClaim = Assert.Single(token.Claims, c => c.Type == ClaimTypes.Name);
		Assert.Equal(expectedSubject, nameClaim.Value);

		// --- Configuration claims (test harness values) ---
		Assert.Equal(TestIssuer, token.Issuer);
		string audience = Assert.Single(token.Audiences);
		Assert.Equal(TestAudience, audience);

		// --- Role claims (login endpoint uses ClaimTypes.Role → long URI in the JWT) ---
		List<string> actualRoles = token.Claims
			.Where(c => c.Type == ClaimTypes.Role)
			.Select(c => c.Value)
			.ToList();
		Assert.Equal(expectedRoles.Length, actualRoles.Count);

		foreach (string role in expectedRoles)
		{
			Assert.Contains(role, actualRoles);
		}

		// --- Token ID — non-empty 32-char hex GUID ---
		Assert.NotNull(token.Id);
		Assert.Matches("^[0-9a-f]{32}$", token.Id);

		// --- Temporal claims (derived from issuance time + configured lifetime) ---
		Assert.Equal(issuedAtUtc, token.ValidFrom);
		Assert.Equal(issuedAtUtc.AddMinutes(TestAccessTokenLifetimeMinutes), token.ValidTo);

		// --- Exact claim-type set — no unexpected claims leak in ---
		// The login endpoint uses ClaimTypes.Name and ClaimTypes.Role (long URIs),
		// while sub/jti/iss/aud/nbf/exp use short JWT registered names.
		var expectedTypes = new SortedSet<string>(StringComparer.Ordinal)
		{
			"aud", "exp", "iss", "jti", "nbf", "sub", ClaimTypes.Name
		};

		if (expectedRoles.Length > 0)
			expectedTypes.Add(ClaimTypes.Role);

		string[] actualTypes = token.Claims.Select(c => c.Type).Distinct().Order().ToArray();
		Assert.Equal(expectedTypes, actualTypes);

		return token;
	}

	/// <summary>
	/// Asserts that the response is a <c>401 Unauthorized</c> with an RFC 7807 ProblemDetails body
	/// containing the LumaCore-specific error type URN, title, and detail message, as produced by
	/// <see cref="MiddlewareIntegration.UseErrorHandlingFeature"/>.
	/// </summary>
	/// <param name="response">The HTTP response to verify.</param>
	private static async Task AssertUnauthorizedProblemDetailsAsync(HttpResponseMessage response)
	{
		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

		ProblemDetails? problem = await response.Content
			                          .ReadFromJsonAsync<ProblemDetails>()
			                          .ConfigureAwait(false);
		Assert.NotNull(problem);
		Assert.Equal(401, problem.Status);
		Assert.Equal(ErrorTypes.Unauthorized, problem.Type);
		Assert.Equal("Authentication Required", problem.Title);
		Assert.Equal("Valid credentials are required to access this resource.", problem.Detail);
	}

	/// <summary>
	/// Asserts that an <see cref="V1.AuthWhoAmIResponse"/> body contains the expected identity
	/// information for the seeded test account: <see cref="TestUsername"/> as <c>Name</c>,
	/// a single <see cref="TestRole"/> role, and the corresponding <see cref="ClaimTypes.Name"/>
	/// and <see cref="ClaimTypes.Role"/> claims.
	/// </summary>
	/// <param name="body">The deserialized WhoAmI response body.</param>
	private static void AssertWhoAmIResponse(V1.AuthWhoAmIResponse body)
	{
		Assert.Equal(TestUsername, body.Name);

		string role = Assert.Single(body.Roles);
		Assert.Equal(TestRole, role);

		Assert.Contains(body.Claims, c => c is (ClaimTypes.Name, TestUsername));
		Assert.Contains(body.Claims, c => c is (ClaimTypes.Role, TestRole));
	}

	/// <summary>
	/// Asserts that an <see cref="V1.AuthIntrospectResponse"/> body contains the expected token
	/// diagnostics for the seeded test account: identity fields (<c>Subject</c>, <c>Name</c>,
	/// <c>Roles</c>), configuration fields (<c>Issuer</c>, <c>Audience</c>,
	/// <c>ConfiguredAccessTokenLifetimeMinutes</c>), a valid <c>JwtId</c>, and deterministic
	/// temporal claims derived from the <paramref name="timeProvider"/>.
	/// </summary>
	/// <param name="body">The deserialized introspect response body.</param>
	/// <param name="timeProvider">
	/// The <see cref="FakeTimeProvider"/> from the test harness, used to compute the expected
	/// <c>NotBeforeUtc</c>, <c>ExpiresUtc</c>, and <c>ExpiresIn</c> values.
	/// </param>
	private static void AssertIntrospectResponse(
		V1.AuthIntrospectResponse body,
		FakeTimeProvider          timeProvider)
	{
		// --- Identity ---
		Assert.Equal(TestUsername, body.Subject);
		Assert.Equal(TestUsername, body.Name);

		string role = Assert.Single(body.Roles);
		Assert.Equal(TestRole, role);

		// --- Configuration ---
		Assert.Equal(TestIssuer, body.Issuer);
		Assert.Equal(TestAudience, body.Audience);
		Assert.Equal(TestAccessTokenLifetimeMinutes, body.ConfiguredAccessTokenLifetimeMinutes);

		// --- Token ID — non-empty 32-char hex GUID ---
		Assert.NotNull(body.JwtId);
		Assert.Matches("^[0-9a-f]{32}$", body.JwtId);

		// --- Temporal claims — deterministic because FakeTimeProvider does not advance ---
		DateTime issuedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
		DateTime expectedExpiresUtc = issuedAtUtc.AddMinutes(TestAccessTokenLifetimeMinutes);

		Assert.Equal(issuedAtUtc, body.NotBeforeUtc);
		Assert.Equal(expectedExpiresUtc, body.ExpiresUtc);
		Assert.Equal(TimeSpan.FromMinutes(TestAccessTokenLifetimeMinutes), body.ExpiresIn);
	}

	/// <summary>
	/// Logs in with the built-in admin credentials and returns the access token.
	/// </summary>
	/// <param name="client">The <see cref="HttpClient"/> connected to the test server.</param>
	/// <returns>The issued JWT access token.</returns>
	private static async Task<string> LoginAsync(HttpClient client)
	{
		var request = new V1.LoginRequest
		{
			Username = TestUsername,
			Password = TestPassword
		};

		HttpResponseMessage response = await client
			                               .PostAsJsonAsync("/api/v1/auth/login", request)
			                               .ConfigureAwait(false);

		response.EnsureSuccessStatusCode();

		V1.LoginResponse? body = await response.Content
			                         .ReadFromJsonAsync<V1.LoginResponse>()
			                         .ConfigureAwait(false);

		return body!.AccessToken;
	}

	/// <summary>
	/// Creates an <see cref="HttpRequestMessage"/> with the <c>Authorization: Bearer</c> header set.
	/// </summary>
	/// <param name="method">The HTTP method.</param>
	/// <param name="requestUri">The request URI.</param>
	/// <param name="token">The JWT access token.</param>
	private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string requestUri, string token)
	{
		var request = new HttpRequestMessage(method, requestUri);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return request;
	}
}

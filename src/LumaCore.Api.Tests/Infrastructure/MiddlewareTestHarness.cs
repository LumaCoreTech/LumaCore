// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace LumaCore.Api.Tests.Infrastructure;

/// <summary>
/// A lightweight, reusable test harness for middleware integration tests that provides an in-memory
/// <see cref="TestServer"/> and <see cref="HttpClient"/> with fully configurable services and pipeline.
/// </summary>
/// <remarks>
///     <para>
///     This harness eliminates the common <see cref="WebApplicationBuilder"/> + <see cref="TestServer"/> ceremony
///     that every middleware integration test requires. The caller retains full control over service registration,
///     middleware ordering, and endpoint mapping through the two delegate parameters of <see cref="CreateAsync"/>.
///     </para>
///     <para>
///         <b>Built-in defaults:</b>
///     </para>
///     <list type="bullet">
///         <item>
///         <c>TestServerWebHostBuilderExtensions.UseTestServer()</c> is applied automatically.
///         </item>
///         <item>
///         <see cref="Environments.Production"/> is used to avoid <c>ValidateOnBuild</c> /
///         <c>ValidateScopes</c> issues that some test runners trigger by injecting
///         <c>ASPNETCORE_ENVIRONMENT=Development</c>.
///         </item>
///     </list>
/// </remarks>
sealed class MiddlewareTestHarness : IAsyncDisposable
{
	private readonly WebApplication mApp;

	/// <summary>
	/// Gets the <see cref="HttpClient"/> connected to the in-memory <see cref="TestServer"/>.
	/// </summary>
	public HttpClient Client { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="MiddlewareTestHarness"/> class.
	/// Use <see cref="CreateAsync"/> to build an instance.
	/// </summary>
	/// <param name="app">The built and started <see cref="WebApplication"/>.</param>
	/// <param name="client">The <see cref="HttpClient"/> connected to the <see cref="TestServer"/>.</param>
	private MiddlewareTestHarness(WebApplication app, HttpClient client)
	{
		mApp = app;
		Client = client;
	}

	/// <summary>
	/// Creates a new harness backed by a <see cref="TestServer"/> with the specified builder and pipeline
	/// configuration.
	/// </summary>
	/// <param name="configureBuilder">
	/// Configures services, configuration sources, and other builder-level options. Called after
	/// <c>TestServerWebHostBuilderExtensions.UseTestServer()</c> is applied, so the caller does not need
	/// to
	/// register it manually.
	/// </param>
	/// <param name="configurePipeline">
	/// Configures the middleware pipeline and endpoint mapping. Called after
	/// <see cref="WebApplicationBuilder.Build"/>. The caller is responsible for middleware ordering (e.g.,
	/// <c>UseRouting()</c> placement) and endpoint mapping.
	/// </param>
	/// <returns>A disposable harness ready for HTTP requests.</returns>
	public static async Task<MiddlewareTestHarness> CreateAsync(
		Action<WebApplicationBuilder> configureBuilder,
		Action<WebApplication>        configurePipeline)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(
			new WebApplicationOptions
			{
				EnvironmentName = Environments.Production
			});
		builder.WebHost.UseTestServer();

		configureBuilder(builder);

		WebApplication app = builder.Build();

		configurePipeline(app);

		await app.StartAsync().ConfigureAwait(false);

		return new MiddlewareTestHarness(app, app.GetTestClient());
	}

	/// <summary>
	/// Stops the application, disposes the <see cref="HttpClient"/>, and releases all resources held by the
	/// in-memory <see cref="TestServer"/>.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await mApp.StopAsync().ConfigureAwait(false);
		await mApp.DisposeAsync().ConfigureAwait(false);
	}
}

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

using SourceGit.DevSpaces.Terminal;

namespace SourceGit.Mcp
{
    public sealed class SourceGitMcpHost : IAsyncDisposable
    {
        public SourceGitMcpHost(DevSpaceTerminalRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public bool IsRunning => _app != null;

        public string LastError { get; private set; } = string.Empty;

        public string BaseAddress { get; private set; } = string.Empty;

        public string SseEndpoint => string.IsNullOrEmpty(BaseAddress)
            ? string.Empty
            : $"{BaseAddress.TrimEnd('/')}/sse";

        public static string GetBaseAddress(SourceGitMcpOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            return $"http://127.0.0.1:{options.Port}";
        }

        public static string GetSseEndpoint(SourceGitMcpOptions options)
        {
            return $"{GetBaseAddress(options)}/sse";
        }

        public static void ConfigureTransport(HttpServerTransportOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.SessionMode = HttpServerSessionMode.Stateful;
#pragma warning disable MCP9004 // Legacy SSE is intentionally required by the SourceGit MCP feature.
            options.EnableLegacySse = true;
#pragma warning restore MCP9004
        }

        public static bool IsAuthorized(SourceGitMcpOptions options, string authorization)
        {
            if (options == null || string.IsNullOrEmpty(options.AuthToken) || string.IsNullOrEmpty(authorization))
                return false;

            const string prefix = "Bearer ";
            if (!authorization.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            var supplied = authorization.AsSpan(prefix.Length);
            var expected = options.AuthToken.AsSpan();
            if (supplied.Length != expected.Length)
                return false;

            var suppliedBytes = Encoding.UTF8.GetBytes(supplied.ToString());
            var expectedBytes = Encoding.UTF8.GetBytes(expected.ToString());
            return CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
        }

        public async Task<bool> StartAsync(
            SourceGitMcpOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            WebApplication app = null;
            try
            {
                if (_app != null)
                    return true;

                LastError = string.Empty;
                BaseAddress = string.Empty;

                if (options.Port < 0 || options.Port > 65535)
                {
                    LastError = "MCP port must be between 0 and 65535.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(options.AuthToken))
                {
                    LastError = "MCP authentication token is required.";
                    return false;
                }

                if (options.MaxConcurrentToolCalls <= 0)
                {
                    LastError = "MCP concurrent tool call limit must be greater than zero.";
                    return false;
                }

                var requestLimiter = new SourceGitMcpRequestLimiter(options.MaxConcurrentToolCalls);
                var builder = WebApplication.CreateSlimBuilder();
                builder.Logging.ClearProviders();
                builder.WebHost.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
                builder.WebHost.UseUrls(GetBaseAddress(options));

                builder.Services.AddSingleton(_registry);
                builder.Services.AddSingleton(options);
                builder.Services
                    .AddMcpServer()
                    .WithHttpTransport(ConfigureTransport)
                    .WithTools<SourceGitMcpTools>();

                app = builder.Build();
                app.Use(async (context, next) =>
                {
                    if (!IsLoopbackHost(context.Request.Host.Host))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }

                    if (!IsAuthorized(options, context.Request.Headers.Authorization.ToString()))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return;
                    }

                    if (HttpMethods.IsPost(context.Request.Method))
                    {
                        if (!requestLimiter.TryEnter(out var lease))
                        {
                            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                            return;
                        }

                        using (lease)
                            await next().ConfigureAwait(false);
                        return;
                    }

                    await next().ConfigureAwait(false);
                });
                app.MapMcp();

                await app.StartAsync(cancellationToken).ConfigureAwait(false);

                var addresses = app.Services
                    .GetRequiredService<IServer>()
                    .Features
                    .Get<IServerAddressesFeature>()?
                    .Addresses;
                BaseAddress = addresses?
                    .FirstOrDefault(x => x.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase))?
                    .TrimEnd('/') ?? GetBaseAddress(options);

                _app = app;
                app = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                BaseAddress = string.Empty;
                return false;
            }
            finally
            {
                if (app != null)
                {
                    try
                    {
                        await app.DisposeAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // A failed optional MCP host must never bring down SourceGit.
                    }
                }

                _gate.Release();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var app = _app;
                if (app == null)
                    return;

                _app = null;
                BaseAddress = string.Empty;

                try
                {
                    await app.StopAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await app.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            _gate.Dispose();
        }

        private static bool IsLoopbackHost(string host)
        {
            return host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }

        private readonly DevSpaceTerminalRegistry _registry;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private WebApplication _app;
    }
}

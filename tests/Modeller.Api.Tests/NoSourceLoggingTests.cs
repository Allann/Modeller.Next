using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modeller.Api.Contracts;
using Xunit;

namespace Modeller.Api.Tests;

/// <summary>
/// Verifies the API's no-source-logging guarantee: a submitted document's content (or an
/// identity registry) must never appear in captured log output, even when analysis fails.
/// </summary>
public sealed class NoSourceLoggingTests
{
    [Fact]
    public async Task Submitted_document_content_never_appears_in_log_output()
    {
        var captured = new List<string>();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(new CapturingLoggerProvider(captured));
            }));
        using var client = factory.CreateClient();
        var marker = $"UniqueMarker{Guid.NewGuid():N}";
        var request = new WorkspaceAnalyzeRequest(
            [new("model/context.rml", $"rml 1.0\ncontext {marker}\n  version 1.0.0\nend\n")],
            new EphemeralIdentityDto(), new ConfigurationDto("1.0", "generated/"), null);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(captured, message => message.Contains(marker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Submitted_document_content_never_appears_in_log_output_even_when_analysis_fails()
    {
        var captured = new List<string>();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(new CapturingLoggerProvider(captured));
            }));
        using var client = factory.CreateClient();
        var marker = $"UniqueMarker{Guid.NewGuid():N}";
        var request = new WorkspaceAnalyzeRequest(
            [new("model/context.rml", $"rml 1.0\ncontext {marker}\n  version 1.0.0\n")], // missing 'end' — malformed
            new EphemeralIdentityDto(), new ConfigurationDto("1.0", "generated/"), null);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(captured, message => message.Contains(marker, StringComparison.Ordinal));
    }

    private sealed class CapturingLoggerProvider(List<string> captured) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(captured);
        public void Dispose() { }

        private sealed class CapturingLogger(List<string> captured) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                lock (captured) captured.Add(message);
            }
        }
    }
}

using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Modeller.Api.Analytics;
using Xunit;

namespace Modeller.Api.Tests;

public sealed class ProductAnalyticsTests
{
    [Fact]
    public async Task Capture_excludes_unapproved_properties_and_raw_Initiative_identifier()
    {
        var handler = new RecordingHandler();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Analytics-Id"] = "anonymous-browser-123456";
        var analytics = new PostHogProductAnalytics(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
            new HttpContextAccessor { HttpContext = context },
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ProductAnalytics:ProjectKey"] = "public-key" }).Build(),
            NullLogger<PostHogProductAnalytics>.Instance);
        var initiativeId = Guid.Parse("db47f60b-9c49-4fbc-8968-bffcf317dc5b");

        await analytics.CaptureAsync(ProductEvents.InitiativeViewed, initiativeId,
            new Dictionary<string, object?> { ["viewer_role"] = "Facilitator", ["response_text"] = "private content" },
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain("private content", handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("response_text", handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(initiativeId.ToString(), handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("viewer_role", handler.Body, StringComparison.Ordinal);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

using Xunit;

namespace Modeller.Api.Tests;

public sealed class ListenUrlsTests
{
    [Fact]
    public void Resolve_binds_the_platform_port_when_one_is_set()
    {
        Assert.Equal("http://0.0.0.0:9000", ListenUrls.Resolve("9000", "https://localhost:62701"));
    }

    [Fact]
    public void Resolve_keeps_the_launch_profile_urls_when_no_port_is_set()
    {
        Assert.Null(ListenUrls.Resolve(null, "https://localhost:62701;http://localhost:62702"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void Resolve_falls_back_to_8080_when_nothing_is_configured(string? port, string? urls)
    {
        Assert.Equal(ListenUrls.FallbackUrl, ListenUrls.Resolve(port, urls));
    }
}

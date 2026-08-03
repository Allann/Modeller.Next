using Modeller.Workspace;
using Xunit;

namespace Modeller.Workspace.Tests;

public sealed class LogicalPathTests
{
    [Theory]
    [InlineData("entities/customer.rml")]
    [InlineData("customer.rml")]
    [InlineData("a/b/c.rml")]
    public void TryCreate_accepts_a_confined_relative_path(string candidate)
    {
        Assert.True(LogicalPath.TryCreate(candidate, out var path));
        Assert.Equal(candidate, path.Value);
    }

    [Fact]
    public void TryCreate_normalizes_backslashes_to_forward_slashes()
    {
        Assert.True(LogicalPath.TryCreate("entities\\customer.rml", out var path));
        Assert.Equal("entities/customer.rml", path.Value);
    }

    [Theory]
    [InlineData("/entities/customer.rml")]
    [InlineData("\\entities\\customer.rml")]
    [InlineData("C:/entities/customer.rml")]
    [InlineData("C:\\entities\\customer.rml")]
    public void TryCreate_rejects_a_rooted_path(string candidate)
    {
        Assert.False(LogicalPath.TryCreate(candidate, out _));
    }

    [Theory]
    [InlineData("../customer.rml")]
    [InlineData("entities/../../customer.rml")]
    [InlineData("entities/..")]
    public void TryCreate_rejects_a_path_that_escapes_via_dot_dot_segments(string candidate)
    {
        Assert.False(LogicalPath.TryCreate(candidate, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryCreate_rejects_blank_or_null_paths(string? candidate)
    {
        Assert.False(LogicalPath.TryCreate(candidate, out _));
    }

    [Fact]
    public void TryCreate_rejects_a_path_containing_a_nul_byte()
    {
        Assert.False(LogicalPath.TryCreate("entities/cus\0tomer.rml", out _));
    }

    [Fact]
    public void Create_throws_for_an_unconfined_path()
    {
        Assert.Throws<ArgumentException>(() => LogicalPath.Create("../escape.rml"));
    }

    [Fact]
    public void Create_returns_a_confined_path()
    {
        var path = LogicalPath.Create("entities/customer.rml");

        Assert.Equal("entities/customer.rml", path.Value);
    }

    [Fact]
    public void Equal_logical_paths_compare_equal_as_a_value_type()
    {
        Assert.Equal(LogicalPath.Create("a/b.rml"), LogicalPath.Create("a/b.rml"));
    }

    [Fact]
    public void Implicit_conversion_to_string_returns_the_confined_value()
    {
        string value = LogicalPath.Create("a/b.rml");

        Assert.Equal("a/b.rml", value);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modeller.Api.Tests;

/// <summary>Mirrors the server's <c>ConfigureHttpJsonOptions</c> (Program.cs) so test requests and
/// responses round-trip <c>ViewKind</c> as the same stable string the server actually sends.</summary>
internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

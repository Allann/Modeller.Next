using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Modeller.Contexts;

public static partial class ContextPackageSystem
{
    private static byte[] Canonicalize(JsonElement element, bool semantic = true)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, element, null, semantic);
        }

        return stream.ToArray();
    }

    private static void WriteCanonical(
        Utf8JsonWriter writer,
        JsonElement element,
        string? propertyName,
        bool semantic)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .Where(property => !IsExcluded(property.Name, propertyName, semantic))
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value, property.Name, semantic);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in CanonicalItems(element, propertyName))
                {
                    WriteCanonical(writer, item, null, semantic);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static IEnumerable<JsonElement> CanonicalItems(JsonElement array, string? propertyName)
    {
        var items = array.EnumerateArray().ToArray();
        if (propertyName is not ("definitions" or "exports" or "inputFacts" or "stages" or "fields" or "relationships" or "members" or
            "conclusions" or "outcomes" or "effects" or "publishedEvents" or "transitions" or "ruleBindings"))
        {
            return items;
        }

        return items.OrderBy(CanonicalArrayKey, StringComparer.Ordinal);
    }

    private static string CanonicalArrayKey(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            return item.GetString()!;
        }

        if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var id))
        {
            return id.GetString()!;
        }

        return Encoding.UTF8.GetString(Canonicalize(item));
    }

    private static bool IsExcluded(string propertyName, string? containerName, bool semantic) =>
        propertyName is "layout" or "layoutState" or "provenance" or "sourceProvenance" or
            "packageDigest" or "semanticDigest" || semantic &&
            (propertyName is "schemaVersion" or "versionRange" ||
             containerName == "context" && propertyName == "version");

    private static string Digest(ReadOnlySpan<byte> value) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(value))}";
}

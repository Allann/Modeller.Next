using System.Text;
using System.Text.Json;
using Xunit;

namespace Modeller.LanguageServer.Tests;

public sealed class RmlLspServerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string FixtureUri = "file:///workspace/accs-eligibility.modeller";
    private static readonly string FixtureText =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller"));

    private static (int Line, int Character) Position(string source, string value)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var line = Array.FindIndex(lines, item => item.Contains(value, StringComparison.Ordinal));
        return (line, lines[line].IndexOf(value, StringComparison.Ordinal) + (value.StartsWith('"') ? 1 : 0));
    }

    private static byte[] Frame(object message)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");
        return [.. header, .. payload];
    }

    private static MemoryStream InputOf(params object[] messages)
    {
        var stream = new MemoryStream();
        foreach (var message in messages)
        {
            var frame = Frame(message);
            stream.Write(frame, 0, frame.Length);
        }
        stream.Position = 0;
        return stream;
    }

    private static List<JsonElement> ReadFrames(MemoryStream output)
    {
        var frames = new List<JsonElement>();
        var bytes = output.ToArray();
        var offset = 0;
        while (offset < bytes.Length)
        {
            var text = Encoding.ASCII.GetString(bytes, offset, bytes.Length - offset);
            var terminator = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            var headerText = text[..terminator];
            var lengthLine = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            var length = int.Parse(lengthLine[(lengthLine.IndexOf(':') + 1)..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            var payloadStart = offset + terminator + 4;
            using var document = JsonDocument.Parse(bytes.AsMemory(payloadStart, length));
            frames.Add(document.RootElement.Clone());
            offset = payloadStart + length;
        }
        return frames;
    }

    private static async Task<List<JsonElement>> RunAsync(MemoryStream input)
    {
        var output = new MemoryStream();
        var server = new global::RmlLspServer(input, output);
        await server.RunAsync();
        return ReadFrames(output);
    }

    private static object DidOpenNotification(string uri = FixtureUri, string? text = null, long version = 1) => new
    {
        jsonrpc = "2.0",
        method = "textDocument/didOpen",
        @params = new { textDocument = new { uri, version, text = text ?? FixtureText } }
    };

    private static JsonElement ResponseFor(List<JsonElement> frames, int id) =>
        frames.Single(frame => frame.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.Number && value.GetInt32() == id);

    [Fact]
    public async Task Initialize_returns_server_capabilities_payload()
    {
        var frames = await RunAsync(InputOf(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { } }));

        var response = Assert.Single(frames);
        Assert.Equal(1, response.GetProperty("id").GetInt32());
        var capabilities = response.GetProperty("result").GetProperty("capabilities");
        Assert.True(capabilities.GetProperty("hoverProvider").GetBoolean());
        Assert.True(capabilities.GetProperty("definitionProvider").GetBoolean());
        Assert.Equal("Modeller RML Language Server", response.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Shutdown_responds_and_exit_after_shutdown_sets_exit_code()
    {
        var savedExitCode = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 42;
            var frames = await RunAsync(InputOf(
                new { jsonrpc = "2.0", id = 1, method = "shutdown", @params = new { } },
                new { jsonrpc = "2.0", method = "exit", @params = new { } }));

            var response = Assert.Single(frames);
            Assert.Equal(1, response.GetProperty("id").GetInt32());
            Assert.True(response.TryGetProperty("result", out _));
            Assert.Equal(0, Environment.ExitCode);
        }
        finally
        {
            Environment.ExitCode = savedExitCode;
        }
    }

    [Fact]
    public async Task Exit_without_prior_shutdown_does_not_touch_the_exit_code()
    {
        var savedExitCode = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 42;
            var frames = await RunAsync(InputOf(new { jsonrpc = "2.0", method = "exit", @params = new { } }));

            Assert.Empty(frames);
            Assert.Equal(42, Environment.ExitCode);
        }
        finally
        {
            Environment.ExitCode = savedExitCode;
        }
    }

    [Fact]
    public async Task DidOpen_upserts_the_document_and_publishes_diagnostics()
    {
        var frames = await RunAsync(InputOf(DidOpenNotification()));

        var notification = Assert.Single(frames);
        Assert.Equal("textDocument/publishDiagnostics", notification.GetProperty("method").GetString());
        Assert.Equal(FixtureUri, notification.GetProperty("params").GetProperty("uri").GetString());
    }

    [Fact]
    public async Task DidChange_full_text_replacement_is_reflected_in_a_subsequent_document_symbol_request()
    {
        var replacement = FixtureText.Replace("Determine ACCS eligibility", "Assess ACCS eligibility", StringComparison.Ordinal);
        var frames = await RunAsync(InputOf(
            DidOpenNotification(),
            new
            {
                jsonrpc = "2.0",
                method = "textDocument/didChange",
                @params = new { textDocument = new { uri = FixtureUri, version = 2 }, contentChanges = new[] { new { text = replacement } } }
            },
            new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/documentSymbol",
                @params = new { textDocument = new { uri = FixtureUri } }
            }));

        var symbols = ResponseFor(frames, 2).GetProperty("result");
        Assert.Contains(symbols.EnumerateArray(), symbol => symbol.GetProperty("name").GetString() == "Assess ACCS eligibility");
    }

    [Fact]
    public async Task DidChange_ranged_edit_replaces_only_the_targeted_span()
    {
        var (line, _) = Position(FixtureText, "version 1.0.0");
        var declarationLine = FixtureText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')[line];
        var start = declarationLine.IndexOf("1.0.0", StringComparison.Ordinal);
        var end = start + "1.0.0".Length;

        var frames = await RunAsync(InputOf(
            DidOpenNotification(),
            new
            {
                jsonrpc = "2.0",
                method = "textDocument/didChange",
                @params = new
                {
                    textDocument = new { uri = FixtureUri, version = 2 },
                    contentChanges = new[]
                    {
                        new
                        {
                            range = new { start = new { line, character = start }, end = new { line, character = end } },
                            text = "2.0.0"
                        }
                    }
                }
            },
            new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/documentSymbol",
                @params = new { textDocument = new { uri = FixtureUri } }
            }));

        var secondPublish = frames.Where(f => f.TryGetProperty("method", out var m) && m.GetString() == "textDocument/publishDiagnostics")
            .Last();
        Assert.Empty(secondPublish.GetProperty("params").GetProperty("diagnostics").EnumerateArray());

        var symbols = ResponseFor(frames, 2).GetProperty("result");
        Assert.Contains(symbols.EnumerateArray(), symbol => symbol.GetProperty("name").GetString() == "Determine ACCS eligibility");
    }

    [Fact]
    public async Task DidClose_removes_the_document_so_later_requests_no_longer_see_it()
    {
        const string otherUri = "file:///workspace/keep-open.modeller";
        const string otherText = "rml 1.0\n# @id=0191f6d4-4ea0-7000-8000-0000000000ff\ncontext Keep Open\n  version 1.0.0\nend\n";

        var frames = await RunAsync(InputOf(
            DidOpenNotification(),
            DidOpenNotification(otherUri, otherText),
            new { jsonrpc = "2.0", method = "textDocument/didClose", @params = new { textDocument = new { uri = FixtureUri } } },
            new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/documentSymbol",
                @params = new { textDocument = new { uri = FixtureUri } }
            }));

        var symbols = ResponseFor(frames, 2).GetProperty("result");
        Assert.Empty(symbols.EnumerateArray());
    }

    [Fact]
    public async Task Completion_returns_a_well_formed_response()
    {
        var (line, character) = Position(FixtureText, "rule Determine");
        var frames = await RunAsync(InputOf(
            DidOpenNotification(),
            new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/completion",
                @params = new { textDocument = new { uri = FixtureUri }, position = new { line, character = character + "rule Det".Length } }
            }));

        var result = ResponseFor(frames, 2).GetProperty("result");
        Assert.False(result.GetProperty("isIncomplete").GetBoolean());
        Assert.Contains(result.GetProperty("items").EnumerateArray(), item => item.GetProperty("label").GetString() == "Determine ACCS eligibility");
    }

    [Fact]
    public async Task Hover_returns_the_symbol_title_and_range()
    {
        var (line, character) = Position(FixtureText, "\"Determine ACCS eligibility\"");
        var frames = await RunAsync(InputOf(
            DidOpenNotification(),
            new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/hover",
                @params = new { textDocument = new { uri = FixtureUri }, position = new { line, character } }
            }));

        var result = ResponseFor(frames, 2).GetProperty("result");
        Assert.Contains("Determine ACCS eligibility", result.GetProperty("contents").GetProperty("value").GetString());
    }

    [Fact]
    public async Task Definition_returns_the_declaration_location_for_a_reference()
    {
        var (line, character) = Position(FixtureText, "\"Determine ACCS eligibility\"");
        var (declarationLine, _) = Position(FixtureText, "rule Determine ACCS eligibility");
        var frames = await RunAsync(InputOf(
            DidOpenNotification(),
            new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/definition",
                @params = new { textDocument = new { uri = FixtureUri }, position = new { line, character } }
            }));

        var result = ResponseFor(frames, 2).GetProperty("result");
        Assert.Equal(FixtureUri, result.GetProperty("uri").GetString());
        Assert.Equal(declarationLine, result.GetProperty("range").GetProperty("start").GetProperty("line").GetInt32());
    }

    [Fact]
    public async Task References_returns_the_declaration_and_at_least_one_usage()
    {
        var (line, character) = Position(FixtureText, "\"Determine ACCS eligibility\"");
        var frames = await RunAsync(InputOf(
            DidOpenNotification(),
            new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/references",
                @params = new { textDocument = new { uri = FixtureUri }, position = new { line, character } }
            }));

        var result = ResponseFor(frames, 2).GetProperty("result");
        Assert.True(result.EnumerateArray().Count() >= 2);
    }

    [Fact]
    public async Task Rename_returns_edits_for_the_document_containing_the_symbol()
    {
        var (line, character) = Position(FixtureText, "\"Determine ACCS eligibility\"");
        var frames = await RunAsync(InputOf(
            DidOpenNotification(),
            new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/rename",
                @params = new { textDocument = new { uri = FixtureUri }, position = new { line, character }, newName = "Confirm ACCS eligibility" }
            }));

        var result = ResponseFor(frames, 2).GetProperty("result");
        var edits = result.GetProperty("changes").GetProperty(FixtureUri);
        Assert.Contains("Confirm ACCS eligibility", Assert.Single(edits.EnumerateArray()).GetProperty("newText").GetString());
    }

    [Fact]
    public async Task Rename_at_a_position_with_no_symbol_returns_an_internal_error()
    {
        var frames = await RunAsync(InputOf(
            DidOpenNotification(),
            new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/rename",
                @params = new { textDocument = new { uri = FixtureUri }, position = new { line = 0, character = 0 }, newName = "Whatever" }
            }));

        var response = ResponseFor(frames, 2);
        Assert.Equal(-32603, response.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task DocumentSymbol_returns_the_symbols_declared_in_the_document()
    {
        var frames = await RunAsync(InputOf(
            DidOpenNotification(),
            new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/documentSymbol",
                @params = new { textDocument = new { uri = FixtureUri } }
            }));

        var result = ResponseFor(frames, 2).GetProperty("result");
        Assert.Contains(result.EnumerateArray(), symbol => symbol.GetProperty("name").GetString() == "Determine ACCS eligibility");
    }

    [Fact]
    public async Task SemanticTokens_full_returns_delta_encoded_token_data_including_comment_lines()
    {
        var frames = await RunAsync(InputOf(
            DidOpenNotification(),
            new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/semanticTokens/full",
                @params = new { textDocument = new { uri = FixtureUri } }
            }));

        var result = ResponseFor(frames, 2).GetProperty("result");
        var data = result.GetProperty("data").EnumerateArray().Select(item => item.GetInt32()).ToArray();
        Assert.NotEmpty(data);
        Assert.Contains(1, data);
    }

    [Fact]
    public async Task Unknown_method_returns_method_not_found()
    {
        var frames = await RunAsync(InputOf(new { jsonrpc = "2.0", id = 1, method = "textDocument/unknownThing", @params = new { } }));

        var response = Assert.Single(frames);
        Assert.Equal(-32601, response.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task A_handler_that_throws_returns_an_internal_error_response_only_when_the_request_had_an_id()
    {
        var frames = await RunAsync(InputOf(
            new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "textDocument/completion",
                @params = new { textDocument = new { uri = "file:///never-opened.rml" }, position = new { line = 0, character = 0 } }
            }));

        var response = Assert.Single(frames);
        Assert.Equal(-32603, response.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task A_notification_handler_that_throws_produces_no_response_because_it_has_no_id()
    {
        var frames = await RunAsync(InputOf(
            new
            {
                jsonrpc = "2.0",
                method = "textDocument/didChange",
                @params = new { textDocument = new { uri = "file:///missing.rml", version = 2 }, contentChanges = new[] { new { text = "x" } } }
            }));

        Assert.Empty(frames);
    }

    [Fact]
    public async Task Malformed_header_without_a_content_length_line_throws()
    {
        var malformed = Encoding.ASCII.GetBytes("X-Not-A-Length: 5\r\n\r\n{}");
        var input = new MemoryStream(malformed);

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(input));
    }

    [Fact]
    public async Task Stream_that_ends_before_the_header_terminator_stops_the_loop_cleanly()
    {
        var partial = Encoding.ASCII.GetBytes("Content-Length: 5\r\n");
        var input = new MemoryStream(partial);

        var frames = await RunAsync(input);

        Assert.Empty(frames);
    }
}

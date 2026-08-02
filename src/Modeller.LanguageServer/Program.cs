using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Modeller.Editor;
using Modeller.Parsing;

var server = new RmlLspServer(Console.OpenStandardInput(), Console.OpenStandardOutput());
await server.RunAsync();

internal sealed class RmlLspServer(Stream input, Stream output)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, RmlWorkspaceDocument> documents = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim writes = new(1, 1);
    private bool shutdown;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await ReadAsync(cancellationToken);
            if (message is null) return;
            var root = message.Value;
            var method = root.TryGetProperty("method", out var methodNode) ? methodNode.GetString() : null;
            var hasId = root.TryGetProperty("id", out var id);
            try
            {
                if (method is not null) await HandleAsync(method, root.GetProperty("params"), hasId ? id : null, cancellationToken);
            }
            catch (Exception)
            {
                if (hasId) await RespondAsync(id, null, new { code = -32603, message = "The language request failed safely." }, cancellationToken);
            }
        }
    }

    private async Task HandleAsync(string method, JsonElement parameters, JsonElement? id, CancellationToken cancellationToken)
    {
        switch (method)
        {
            case "initialize":
                await RespondAsync(id!.Value, new { capabilities = new {
                    textDocumentSync = 2,
                    completionProvider = new { triggerCharacters = new[] { "\"", " " } },
                    hoverProvider = true, definitionProvider = true, referencesProvider = true,
                    renameProvider = new { prepareProvider = false }, documentSymbolProvider = true,
                    semanticTokensProvider = new { legend = new { tokenTypes = new[] { "keyword", "comment" }, tokenModifiers = Array.Empty<string>() }, full = true }
                }, serverInfo = new { name = "Modeller RML Language Server", version = "1.0.0" } }, null, cancellationToken); break;
            case "initialized": break;
            case "shutdown": shutdown = true; await RespondAsync(id!.Value, null, null, cancellationToken); break;
            case "exit": if (shutdown) Environment.ExitCode = 0; return;
            case "textDocument/didOpen":
                Upsert(parameters.GetProperty("textDocument")); await PublishAsync(parameters.GetProperty("textDocument").GetProperty("uri").GetString()!, cancellationToken); break;
            case "textDocument/didChange":
                Change(parameters); await PublishAsync(parameters.GetProperty("textDocument").GetProperty("uri").GetString()!, cancellationToken); break;
            case "textDocument/didClose":
                documents.TryRemove(parameters.GetProperty("textDocument").GetProperty("uri").GetString()!, out _); break;
            case "textDocument/completion":
                await RespondAsync(id!.Value, Completion(parameters), null, cancellationToken); break;
            case "textDocument/hover":
                await RespondAsync(id!.Value, Hover(parameters), null, cancellationToken); break;
            case "textDocument/definition":
                await RespondAsync(id!.Value, LocationOrNull(RmlLanguageService.Definition(Analysis(cancellationToken), Uri(parameters), Position(parameters))), null, cancellationToken); break;
            case "textDocument/references":
                await RespondAsync(id!.Value, RmlLanguageService.References(Analysis(cancellationToken), Uri(parameters), Position(parameters)).Select(Location), null, cancellationToken); break;
            case "textDocument/rename":
                await RespondAsync(id!.Value, Rename(parameters, cancellationToken), null, cancellationToken); break;
            case "textDocument/documentSymbol":
                await RespondAsync(id!.Value, DocumentSymbols(parameters, cancellationToken), null, cancellationToken); break;
            case "textDocument/semanticTokens/full":
                await RespondAsync(id!.Value, SemanticTokens(parameters, cancellationToken), null, cancellationToken); break;
            default:
                if (id is not null) await RespondAsync(id.Value, null, new { code = -32601, message = "Method not found." }, cancellationToken); break;
        }
    }

    private object Completion(JsonElement parameters)
    {
        var uri = Uri(parameters); var position = Position(parameters); var document = documents[uri.AbsoluteUri];
        var line = document.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ElementAtOrDefault(position.Line) ?? string.Empty;
        var before = line[..Math.Min(position.Character, line.Length)];
        var quote = before.LastIndexOf('"');
        var prefix = quote >= 0 ? before[(quote + 1)..] : new string(before.Reverse().TakeWhile(char.IsLetter).Reverse().ToArray());
        return new { isIncomplete = false, items = RmlLanguageService.Complete(Analysis(default), prefix).Select(item => new { label = item.Label, kind = item.Kind == "keyword" ? 14 : 6, detail = item.Detail }) };
    }
    private object? Hover(JsonElement parameters)
    {
        var hover = RmlLanguageService.Hover(Analysis(default), Uri(parameters), Position(parameters));
        return hover is null ? null : new { contents = new { kind = "markdown", value = $"**{hover.Title}**\n\n{hover.Detail}" }, range = Range(hover.Range) };
    }
    private object Rename(JsonElement parameters, CancellationToken cancellationToken)
    {
        var analysis = Analysis(cancellationToken); var uri = Uri(parameters); var position = Position(parameters); var newName = parameters.GetProperty("newName").GetString()!;
        var oldName = RmlLanguageService.Hover(analysis, uri, position)?.Title ?? throw new InvalidOperationException("No symbol exists at the rename position.");
        var changes = new Dictionary<string, object[]>();
        foreach (var document in documents.Values)
        {
            var edit = RmlCompiler.Rename(document.Text, oldName, newName);
            if (edit.Changed) changes[document.Uri.AbsoluteUri] = [new { range = Range(Whole(document.Text)), newText = edit.Updated }];
        }
        return new { changes };
    }
    private object DocumentSymbols(JsonElement parameters, CancellationToken cancellationToken)
    {
        var uri = Uri(parameters);
        return Analysis(cancellationToken).Symbols.Where(symbol => symbol.Declaration.Uri == uri).Select(symbol => new { name = symbol.Name, kind = 13, range = Range(symbol.Declaration.Range), selectionRange = Range(symbol.Declaration.Range) });
    }
    private object SemanticTokens(JsonElement parameters, CancellationToken cancellationToken)
    {
        var uri = Uri(parameters); var data = new List<int>(); var lastLine = 0; var lastCharacter = 0;
        foreach (var token in Analysis(cancellationToken).Tokens.Where(token => token.Uri == uri).OrderBy(token => token.Range.Start.Line).ThenBy(token => token.Range.Start.Character))
        {
            var lineDelta = token.Range.Start.Line - lastLine; var characterDelta = lineDelta == 0 ? token.Range.Start.Character - lastCharacter : token.Range.Start.Character;
            data.AddRange([lineDelta, characterDelta, token.Range.End.Character - token.Range.Start.Character, token.Kind == "comment" ? 1 : 0, 0]);
            lastLine = token.Range.Start.Line; lastCharacter = token.Range.Start.Character;
        }
        return new { data };
    }
    private RmlWorkspaceAnalysis Analysis(CancellationToken cancellationToken) => RmlLanguageService.Analyze(documents.Values, cancellationToken);
    private void Upsert(JsonElement node)
    {
        var uri = node.GetProperty("uri").GetString()!;
        documents[uri] = new(new(uri), node.TryGetProperty("version", out var version) ? version.GetInt64() : 0, node.GetProperty("text").GetString()!);
    }
    private void Change(JsonElement parameters)
    {
        var node = parameters.GetProperty("textDocument"); var uri = node.GetProperty("uri").GetString()!;
        var text = documents[uri].Text;
        foreach (var change in parameters.GetProperty("contentChanges").EnumerateArray())
        {
            var replacement = change.GetProperty("text").GetString()!;
            if (!change.TryGetProperty("range", out var range)) { text = replacement; continue; }
            var start = Offset(text, range.GetProperty("start")); var end = Offset(text, range.GetProperty("end"));
            text = string.Concat(text.AsSpan(0, start), replacement, text.AsSpan(end));
        }
        documents[uri] = new(new(uri), node.GetProperty("version").GetInt64(), text);
    }
    private async Task PublishAsync(string uri, CancellationToken cancellationToken)
    {
        var analysis = Analysis(cancellationToken);
        await NotifyAsync("textDocument/publishDiagnostics", new { uri, version = documents[uri].Version, diagnostics = analysis.LocatedDiagnostics.Where(item => item.Uri.AbsoluteUri == uri).Select(item => new { range = Range(item.Diagnostic.Range), severity = 1, code = item.Diagnostic.Code, source = "modeller", message = item.Diagnostic.Message }) }, cancellationToken);
    }
    private async Task<JsonElement?> ReadAsync(CancellationToken cancellationToken)
    {
        var header = new List<byte>(); var one = new byte[1];
        while (true)
        {
            var count = await input.ReadAsync(one, cancellationToken); if (count == 0) return null; header.Add(one[0]);
            if (header.Count >= 4 && header[^4] == '\r' && header[^3] == '\n' && header[^2] == '\r' && header[^1] == '\n') break;
        }
        var text = Encoding.ASCII.GetString([.. header]);
        var lengthLine = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
        var length = int.Parse(lengthLine[(lengthLine.IndexOf(':') + 1)..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        var payload = new byte[length]; var read = 0;
        while (read < length) { var count = await input.ReadAsync(payload.AsMemory(read, length - read), cancellationToken); if (count == 0) return null; read += count; }
        using var document = JsonDocument.Parse(payload); return document.RootElement.Clone();
    }
    private Task NotifyAsync(string method, object parameters, CancellationToken cancellationToken) => WriteAsync(new { jsonrpc = "2.0", method, @params = parameters }, cancellationToken);
    private Task RespondAsync(JsonElement id, object? result, object? error, CancellationToken cancellationToken) => WriteAsync(error is null ? new { jsonrpc = "2.0", id, result } : new { jsonrpc = "2.0", id, error }, cancellationToken);
    private async Task WriteAsync(object value, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, Json); var header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");
        await writes.WaitAsync(cancellationToken); try { await output.WriteAsync(header, cancellationToken); await output.WriteAsync(payload, cancellationToken); await output.FlushAsync(cancellationToken); } finally { writes.Release(); }
    }
    private static Uri Uri(JsonElement parameters) => new(parameters.GetProperty("textDocument").GetProperty("uri").GetString()!);
    private static EditorPosition Position(JsonElement parameters) { var node = parameters.GetProperty("position"); return new(node.GetProperty("line").GetInt32(), node.GetProperty("character").GetInt32()); }
    private static object? LocationOrNull(RmlLocation? location) => location is null ? null : new { uri = location.Uri.AbsoluteUri, range = Range(location.Range) };
    private static object Location(RmlLocation location) => new { uri = location.Uri.AbsoluteUri, range = Range(location.Range) };
    private static object Range(EditorRange range) => new { start = new { line = range.Start.Line, character = range.Start.Character }, end = new { line = range.End.Line, character = range.End.Character } };
    private static EditorRange Whole(string text) { var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'); return new(new(0, 0), new(lines.Length - 1, lines[^1].Length)); }
    private static int Offset(string text, JsonElement position)
    {
        var targetLine = position.GetProperty("line").GetInt32(); var targetCharacter = position.GetProperty("character").GetInt32();
        var line = 0; var offset = 0;
        while (line < targetLine && offset < text.Length) { if (text[offset++] == '\n') line++; }
        return Math.Min(text.Length, offset + targetCharacter);
    }
}

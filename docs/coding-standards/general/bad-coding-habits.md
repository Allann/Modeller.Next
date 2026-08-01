---
title: "Encapsulate External Services and Model Flow Explicitly"
---

# Encapsulate External Services and Model Flow Explicitly


## The Standard

Wrap third-party/HTTP APIs behind a dedicated client class instead of inlining `HttpClient` calls in `Program.cs` or business logic. Give raw text/data a real type (with behavior) instead of passing strings and manipulating them with scattered LINQ/loops. Replace boolean-flag-driven `while` loops with an explicit enum/state machine describing control flow.

## Why

The "before" version mixed HTTP plumbing, retry/pagination logic, markdown parsing, and file-naming into one giant top-level `Program.cs` method using a `bool downloadSucceeded` flag loop and raw `JsonDocument` navigation. This made the retry/skip/quit logic hard to follow, hid the "what is a README" concept inside string splitting, and made the method impossible to unit test or reuse. Extracting `GitHubApiClient` (encapsulating auth, headers, pagination as `IAsyncEnumerable`) and `Markdown` (encapsulating heading parsing) turns implicit string munging into explicit, testable, named operations. Replacing the ad-hoc boolean retry flag with an `enum Flow { Continue, Retry, Skip, Abort }` makes every possible outcome of "processing a repo" explicit and switchable.

## Before (Anti-pattern)

```csharp
var rawContent = string.Empty;
bool downloadSucceeded = false;
while (!downloadSucceeded)
{
    var response = await http.SendAsync(request);
    downloadSucceeded = response.IsSuccessStatusCode;
    rawContent = await response.Content.ReadAsStringAsync();
    if (!downloadSucceeded)
    {
        Console.Write("Retry, skip, or quit? (r/s/q): ");
        string? input = Console.ReadLine()?.Trim().ToLower();
        if (input == "q") return saved;
        else if (input != "r") break;
    }
}
var lines = rawContent.Replace("\r", "").Split('\n');
bool hasGettingStarted = lines.Any(line => line.TrimStart().StartsWith('#') && line.Contains("getting started", StringComparison.InvariantCultureIgnoreCase));
```

## After (Standard)

```csharp
enum Flow { Continue, Retry, Skip, Abort };

static async Task<(Flow flow, string savedFile)> ProcessRepo(GitHubApiClient client, string filesDir, string owner, string repoName)
{
    var readme = await client.GetReadme(owner, repoName);
    if (readme is not null && readme.ContainsSection("getting started"))
    {
        var fileName = FileNameFor(owner, repoName);
        await readme.SaveAsync(Path.Combine(filesDir, fileName));
        return (Flow.Continue, fileName);
    }
    return (Flow.Skip, string.Empty);
}
```

## Rules for LLMs / Agents

- Never call `HttpClient` directly from `Program.cs`, top-level statements, or business logic; wrap external HTTP/API calls in a dedicated client class.
- Give raw payload types (markdown, JSON blobs, CSV, etc.) a wrapper type with named query methods (e.g. `ContainsSection`, `GetHeadings`) instead of inline string splitting/LINQ scattered at call sites.
- Do not encode workflow outcomes as loose `bool` flags plus string comparisons (`input == "r"`); use an `enum` or discriminated union naming every outcome and switch/pattern-match on it.
- Prefer `IAsyncEnumerable<T>` for paginated/streaming API results instead of manual `page++` loops mixed with business filtering logic.
- Keep file-naming/sanitization logic as a small pure function (e.g. `FileNameFor`), not inlined at the call site.

## When NOT to apply

Trivial one-off scripts/spikes with a single external call and no retry/pagination logic do not need a dedicated client class — the overhead isn't justified until there's real behavior (auth, pagination, parsing) to encapsulate.

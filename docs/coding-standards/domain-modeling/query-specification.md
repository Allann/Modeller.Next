---
title: "Build External API Queries With a Lightweight Query Specification"
---

# Build External API Queries With a Lightweight Query Specification


## The Standard

When a client method needs to support an open-ended combination of filters and sort orders against an external API (or any query source), do not hard-code the query in the client method. Instead, model the query as an immutable, chainable specification object (`Query.Where(Filter.X()).SortByDescending(Sort.Y)`) built from small factory-method value types (`Filter`, `Sort`), and have the client method accept that specification as a parameter.

## Why

In the "before" version, `GitHubApiClient.QueryReposAsync()` takes no parameters and has the filter (`language:csharp+stars:>10`) and sort (`sort=stars&order=desc`) baked directly into the URL string — any new search need requires editing the client. In the "after" version, `RepoQuery` is an immutable builder (`Where` returns a new `RepoQuery` via a private copy constructor) that accumulates `RepoFilter` and `RepoSort` values and exposes only the derived URL fragments (`QueryClause`, `SortClause`, `OrderClause`) the client needs — the client itself becomes a thin, reusable translator from specification to HTTP request. Callers compose whatever query they need (`new RepoQuery().Where(RepoFilter.Language("C#")).Where(RepoFilter.Stars(11)).SortByDescending(RepoSort.Stars)`) without the client knowing about any specific combination. `RepoFilter` and `RepoSort` themselves hide their string-based wire format behind named static factories (`RepoFilter.Language(...)`, `RepoSort.Stars`), so no call site constructs a raw query-string fragment by hand.

## Before (Anti-pattern)

```csharp
public async IAsyncEnumerable<Repo> QueryReposAsync()
{
    var url = "https://api.github.com/search/repositories?" +
              "q=language:csharp+stars:>10&sort=stars&order=desc&page=1&per_page=10";
    // ... fixed query, cannot be reused for any other search
}
```

## After (Standard)

```csharp
public class RepoQuery
{
    private RepoFilter[] _filters = [];
    private RepoSort? _sort;
    private bool _ascending;

    public RepoQuery Where(RepoFilter filter) => new([.._filters, filter], _sort, _ascending);
    public RepoQuery SortByDescending(RepoSort sort) => new(_filters, sort, false);

    internal string QueryClause => _filters.Length == 0 ? "" : $"q={string.Join("+", _filters.Select(f => f.Filter))}";
}

var query = new RepoQuery()
    .Where(RepoFilter.Language("C#"))
    .Where(RepoFilter.Stars(11))
    .SortByDescending(RepoSort.Stars);

await foreach (var repo in client.QueryReposAsync(query)) { /* ... */ }
```

## Rules for LLMs / Agents

- When a client/repository method's filtering or sorting needs may vary by caller, accept an immutable query/specification object as a parameter instead of parameters per filter or a hard-coded query.
- Make the specification type immutable: every `Where`/`SortBy` call returns a new instance; never mutate `this` in place.
- Hide the wire-format details (query string fragments, SQL fragments, etc.) behind `internal`/`private` members of the specification and filter types; expose only named static factory methods (`RepoFilter.Language(...)`, `RepoSort.Stars`) as the public construction API.
- Keep the specification's consuming method (the API/DB client) ignorant of which filters exist — it should only ask the specification for the derived clauses it needs (`query.QueryClause`, `query.SortClause`) and assemble them, not branch on filter types itself.
- Do not let callers construct raw filter/sort strings inline; every filter or sort option must go through its factory method so the format stays centralized and easy to change.

## When NOT to apply

For a single, truly fixed query that will never vary by caller, a hard-coded query is simpler and this pattern is unnecessary overhead. Introduce the specification once a second distinct filter/sort combination is needed against the same data source.

---
title: "Stream Large JSON Payloads Instead of Buffering Whole Arrays"
---

# Stream Large JSON Payloads Instead of Buffering Whole Arrays


## The Standard

When an endpoint receives (or would otherwise need to fully materialize) a large JSON array before processing it, deserialize it as an `IAsyncEnumerable<T>` via `JsonSerializer.DeserializeAsyncEnumerable<T>` and process each element with `await foreach` as it arrives, instead of binding the whole payload to `T[]`/`List<T>` and looping over it after the fact.

## Why

In the "before" version, the minimal API endpoint bound the request body directly to `Person[] people`, which forces ASP.NET Core to buffer and fully deserialize the entire array before a single element can be processed — memory usage and time-to-first-processed-item both scale with the whole payload, and the client sees a long, opaque delay under load. In the "after" version, the endpoint reads `context.Request.Body` directly and calls `JsonSerializer.DeserializeAsyncEnumerable<Person>(body, jsonOptions)`, then processes each `Person` inside `await foreach` as it is parsed off the wire. Processing starts before the request body has finished arriving, peak memory is bounded by one element instead of the whole array, and the demo's load simulator shows materially better responsiveness under concurrent load with per-item work (e.g. simulated persistence delay).

## Before (Anti-pattern)

```csharp
app.MapPost("/classic", async (Person[] people) =>
{
    foreach (var person in people)
    {
        // Simulate persistence delay or similar
    }
    return Results.Ok();
});
```

## After (Standard)

```csharp
app.MapPost("/streaming", async (HttpContext context, [FromQuery] int? delayUs) =>
{
    using var body = context.Request.Body;

    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    IAsyncEnumerable<Person?> people = JsonSerializer.DeserializeAsyncEnumerable<Person>(body, jsonOptions);

    await foreach (var person in people)
    {
        if (person is null) continue;
        // Simulate persistence delay or similar
    }

    return Results.Ok();
});
```

## Rules for LLMs / Agents

- When an API endpoint or background job must process a JSON array that can be large or unbounded, deserialize it with `JsonSerializer.DeserializeAsyncEnumerable<T>` over the raw request/response stream and process items with `await foreach`, rather than binding to `T[]`/`List<T>`.
- Do not add framework-level model binding (e.g. minimal API parameter binding to an array type) for payloads that are expected to be large; read `HttpContext.Request.Body` explicitly so streaming is possible.
- Each item pulled from the async enumerable must be null-checked (`DeserializeAsyncEnumerable<T>` yields `T?`) before use.
- Keep per-item work inside the `await foreach` loop free of operations that require the full collection up front (e.g. don't call `.ToList()` on the async enumerable just to loop over it — that defeats the purpose).

## When NOT to apply

If the payload is small and bounded (e.g. a handful of fields, a single object, or an array capped at a small, known size), plain buffered deserialization (`[FromBody] T`) is simpler and the streaming approach adds unnecessary complexity for no measurable benefit.

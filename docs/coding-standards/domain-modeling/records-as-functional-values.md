---
title: "Use Records as Functional Values, Not Objects With Hidden State"
---

# Use Records as Functional Values, Not Objects With Hidden State


## The Standard

When a `record` models a value (a video, a clip, a time interval), express all operations on it as pure functions/extension methods that take the record(s) as input and return a new record as output. Do not smuggle mutable, order-dependent state (like a running "clip" offset) into a record and call methods that mutate it in place.

## Why

The mutable `Video` class tracks clipping as internal mutable state (`Beginning`, `Duration`, `FullDuration`) via a stateful `Clip(...)` method that must be paired with `ResetClipping()` before it can be reused — callers have to remember the reset-then-mutate protocol, and every call changes the meaning of "the same" `Video` instance. The functional version keeps `Video` an immutable fact (title + media) and expresses "a video clipped to an interval" as a *new* value, `VideoClip`, produced by a pure extension method `video.Clip(interval)`. Composing behavior is now just LINQ over pure functions (`Select(video.Clip)`) instead of a loop that mutates and resets a shared object each iteration.

## Before (Anti-pattern)

```csharp
public class Video(string title, byte[] content, TimeSpan duration)
{
    private TimeSpan Beginning { get; set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; private set; } = duration;

    public void Clip(TimeSpan beginning, TimeSpan duration) { /* mutates Beginning/Duration */ }
    public void ResetClipping() { Beginning = TimeSpan.Zero; Duration = FullDuration; } // must remember to call this
}

mutableVideo.Clip(beginning, duration);
Console.WriteLine(mutableVideo);
mutableVideo.ResetClipping();   // easy to forget -> next iteration is wrong
```

## After (Standard)

```csharp
public record Video(string Title, VideoMedia Media)
{
    public static Video Create(string title, VideoMedia media) => new(
        string.IsNullOrWhiteSpace(title) ? throw new ArgumentException(nameof(title)) : title, media);
}

public record VideoClip(Video Video, TimeInterval Interval);

public static class Clipping
{
    public static VideoClip Clip(this Video video, TimeInterval clip) =>
        VideoClip.CreateClipped(video, TimeInterval.FromDuration(video.Media.Duration).Clip(clip));
}

TimeIntervals.CreateMany(TimeSpan.Zero, duration, step)
    .TakeWhile(interval => interval.Offset < video.Media.Duration)
    .Select(video.Clip)          // pure, no shared mutable state, safe to reuse `video`
    .ForEach(Console.WriteLine);
```

## Rules for LLMs / Agents

- For value-like concepts, model the operation's *result* as its own record type (`VideoClip`) rather than mutating the original record's conceptual state.
- Implement operations on records as extension methods/static functions of the form `(record, args) => newRecord`, not instance methods that assign to the record's own properties.
- Never require callers to call a "reset" method between uses of the same instance — if resettable state is needed, that is a sign the type should be replaced by producing a fresh value per use.
- Favor composing these pure functions with LINQ (`Select`, `Where`, `Aggregate`) over hand-written loops with mutable accumulator variables.
- Keep constructors validating and side-effect free; expose creation through named static factories (`Create`, `CreateClipped`, `CreateEntire`) that make the semantics of each construction path explicit.

## When NOT to apply

Entities with real identity and lifecycle that must be tracked by EF Core (or another ORM) still need settable/trackable properties at the persistence boundary — see `ef-core-record-type-tracking.md`. Use the functional-record style for in-memory value objects and domain calculations, not for the ORM-facing shape of an entity.

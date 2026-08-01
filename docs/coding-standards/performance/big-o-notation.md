---
title: "Reason About and Document Algorithmic Complexity"
---

# Reason About and Document Algorithmic Complexity


## The Standard

When writing code that processes large or unbounded inputs (streams, collections, files), choose data structures and algorithms with the input's realistic scale in mind, and state the resulting time/space complexity in a comment next to the algorithm. Prefer a fixed-size sliding-window/circular-buffer approach over recomputing over the whole dataset or materializing it fully in memory.

## Why

The demo processes up to an hour of audio (~300 million float samples) to find the quietest 0.5-second region using a fixed-size circular buffer (`float[regionLength]`) and a running sum, giving O(n) time and O(k) space (k = window size, ~40 thousand samples, <200 KB) instead of O(n·k) (recomputing the sum for every window) or O(n) space (buffering the entire decoded signal, ~1.2 GB). The comments in the source explicitly work out `n` and `k` from the problem's real-world constraints before writing the loop — complexity analysis drives the design, not an afterthought.

## Before (Anti-pattern)

```csharp
// Naive: recompute the window sum from scratch for every position -> O(n * k) time
int FindQuietestRegionNaive(float[] sound, int regionLength)
{
    float quietestSum = float.MaxValue;
    int quietestIndex = 0;
    for (int start = 0; start + regionLength <= sound.Length; start++)
    {
        float sum = 0;
        for (int i = start; i < start + regionLength; i++) sum += Math.Abs(sound[i]);
        if (sum < quietestSum) { quietestSum = sum; quietestIndex = start; }
    }
    return quietestIndex;
}
```

## After (Standard)

```csharp
// Sound: 44100 samples/sec/channel x 2 channels x 3600 sec => n = 300 million samples
// Quiet interval: 0.5 sec => k = 40+ thousand samples; buffer size = k * 4 bytes < 200 kB
int FindQuietestRegion(IEnumerable<float> sound, int regionLength)   // O(n) time; O(k) space
{
    float[] buffer = new float[regionLength];                        // O(k) space
    float currentSum = 0, quietestSum = 0;
    int quietestIndex = 0, count = 0;

    foreach (float sample in sound)                                  // O(n) time; O(1) space per step
    {
        if (count >= regionLength) currentSum -= buffer[count % regionLength];
        count += 1;
        float abs = Math.Abs(sample);
        buffer[count % regionLength] = abs;
        currentSum += abs;

        if (count == regionLength) quietestSum = currentSum;
        else if (count > regionLength && currentSum < quietestSum)
            (quietestSum, quietestIndex) = (currentSum, count - regionLength);
    }
    return quietestIndex;
}
```

## Rules for LLMs / Agents

- Before choosing an algorithm over data that can be large (files, streams, DB result sets), estimate the realistic `n` (and any window/subset size `k`) from the domain, and let that drive the choice of algorithm/data structure.
- Prefer streaming (`IEnumerable<T>`/`IAsyncEnumerable<T>`) plus a fixed-size buffer over loading an entire dataset into memory when only a bounded window of it is ever needed at once.
- Avoid nested loops over the same large collection (O(n·k) or O(n²)) when a running/incremental computation (sliding window, prefix sums) can achieve O(n).
- Annotate non-trivial algorithms with a short comment stating their time and space complexity (e.g. `// O(n) time; O(k) space`) so reviewers and future maintainers don't have to re-derive it.
- When complexity depends on a real-world constraint (max recording length, max page size, max item count), write that assumption down next to the algorithm.

## When NOT to apply

For small, bounded collections (e.g. UI lists capped at dozens of items, in-memory config), don't over-engineer sliding-window/streaming solutions — a simple `O(n²)` or full-materialization approach is fine and more readable when `n` is small and fixed.

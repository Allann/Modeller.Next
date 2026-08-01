---
title: "Implement GoF Patterns as Delegates and Higher-Order Functions, Not Class Hierarchies"
---

# Implement GoF Patterns as Delegates and Higher-Order Functions, Not Class Hierarchies


## The Standard

Where a classic object-oriented design pattern is needed (Strategy, Composite, Chain of Responsibility, Specification, Iterator), replace the interface + concrete-implementing-classes structure with a `delegate` (or `Func<T>`) type and static functions/lambdas assigned to it. Compose behavior via higher-order functions that take and return delegates, not via subclassing or object composition.

## Why

Each pattern in the reference implementation is rebuilt around a single idea: **a delegate type stands in for the interface, and a function stands in for the class that would have implemented it.** This removes an entire layer of ceremony (interface declaration, one class per variant, `new` to construct each variant) and makes composition just function composition, which C# already has first-class support for (higher-order functions, lambdas, `yield return`).

## Before (Anti-pattern)

```csharp
// Classic OO Composite: an interface plus one class per node kind
interface IFileSystemComponent
{
    long GetSize();
}

class FileComponent : IFileSystemComponent
{
    private readonly string _path;
    public FileComponent(string path) => _path = path;
    public long GetSize() => new FileInfo(_path).Length;
}

class DirectoryComponent : IFileSystemComponent
{
    private readonly IEnumerable<IFileSystemComponent> _children;
    public DirectoryComponent(IEnumerable<IFileSystemComponent> children) => _children = children;
    public long GetSize() => _children.Sum(c => c.GetSize());
}
```

## After (Standard)

```csharp
// Composite as a delegate + recursive composition — no class hierarchy
delegate long GetFileSize();

static GetFileSize GetTotalFileSizes(params GetFileSize[] fileSizes) =>
    () => fileSizes.Sum(fileSize => fileSize());

static GetFileSize ReadAny(string path) =>
    File.Exists(path) ? ReadFile(path)
    : Directory.Exists(path) ? ReadDirectory(path)
    : NonexistentObjectSize;

static GetFileSize NonexistentObjectSize => () => 0L;

static GetFileSize ReadFile(string path) =>
    () => new FileInfo(path).Length;

static GetFileSize ReadDirectory(string path) =>
    GetTotalFileSizes(path.GetContent().Select(ReadAny).ToArray());
```

## Rules for LLMs / Agents

- Before introducing an interface with a single method plus one implementing class per variant, check whether a `delegate`/`Func<>` type plus static functions/lambdas expresses the same idea with less ceremony.
- Model **Strategy** as `delegate TResult Strategy(...)` with each concrete strategy a lambda assigned to that delegate, not a family of `IStrategy` implementations.
- Model **Composite** as a recursive function returning the same delegate type it consumes (a leaf and a composite are both just functions with that signature), not a `Component`/`Leaf`/`Composite` class tree.
- Model **Chain of Responsibility** as an extension method combining a handler `Func<T,TResult>`, a predicate, and a fallback into a new `Func`, not a linked list of handler objects.
- Model **Specification** with combinators (`And`, `All`) that compose `delegate bool Spec(T)` values recursively (e.g. via list patterns), not `AndSpecification`/`OrSpecification` wrapper classes.
- Model **Iterator** as a `delegate IEnumerable<T> Iterator<T>(T seed)` built with a C# iterator block (`yield return`), not a hand-written `IEnumerator<T>` implementation.

## When NOT to apply

When a pattern genuinely needs per-variant state beyond what a closure can hold cleanly, or when the variants must be independently unit-tested/mocked as substitutable objects via DI, a class-based implementation may still be the better fit.

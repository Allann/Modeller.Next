---
title: "Express Business Rules as Composable, Injectable Rule Objects"
---

# Express Business Rules as Composable, Injectable Rule Objects


## The Standard

When a domain concept is governed by one or more business rules that may vary, combine, or depend on runtime context (the current user, configuration, tenant), model each rule as an implementation of a small rule interface (`bool IsSatisfiedBy(T subject)`, `T ApplyTo(T subject)`), combine multiple rules with a composite (`AllXValidity(params IXValidity[] rules)`), and inject the composed rule as a dependency (e.g., an ASP.NET Core handler parameter) rather than hard-coding the check inline in the code that uses it.

## Why

The "before" `PostBook` handler has no notion of "title validity" beyond a blank-string check baked directly into the handler body — any new title rule (case style, banned words, length) means editing the handler. The "after" version introduces `ITitleValidity` (`IsSatisfiedBy` / `ApplyTo`), lets each rule (`TitleCaseRule`, others) implement it independently, and provides `AllTitleValidity(params ITitleValidity[] rules)` — a composite that is itself an `ITitleValidity`, satisfied only when every wrapped rule is satisfied, and that applies every wrapped rule's correction in sequence via `Aggregate`. The handler receives `ITitleValidity titleValidityRule` as a normal DI parameter and only ever calls `IsSatisfiedBy`/`ApplyTo` — it has zero knowledge of which concrete rules are active or how many there are. This also enables context-dependent rules like `UserRoleTitleRule(Func<ClaimsPrincipal> getUser, string role)`, which decides satisfaction based on the current request's user — something that cannot be expressed as a static validation check baked into the handler or the model.

## Before (Anti-pattern)

```csharp
public static async Task<IResult> PostBook(BookstoreDbContext dbContext, UriHelper uriHelper,
    BookTitleToSlug titleToSlug, [FromBody] PostBookRequest book)
{
    string title = book.Title ?? string.Empty;
    // Only rule: not empty — checked once, inline, unextendable
    if (string.IsNullOrWhiteSpace(title))
        validationErrors.AddFieldValidationError(nameof(book.Title), "Title required");
}
```

## After (Standard)

```csharp
public interface ITitleValidity
{
    bool IsSatisfiedBy(BookTitle title);
    BookTitle ApplyTo(BookTitle title);
}

public class AllTitleValidity(params ITitleValidity[] rules) : ITitleValidity
{
    public bool IsSatisfiedBy(BookTitle title) => rules.All(rule => rule.IsSatisfiedBy(title));
    public BookTitle ApplyTo(BookTitle title) => rules.Aggregate(title, (result, rule) => rule.ApplyTo(result));
}

public static async Task<IResult> PostBook(/* ... */, ITitleValidity titleValidityRule)
{
    BookTitle title = new(book.Title ?? string.Empty);
    if (!titleValidityRule.IsSatisfiedBy(title))
    {
        title = titleValidityRule.ApplyTo(title);
        validationErrors.AddFieldValidationError(nameof(book.Title), $"Invalid title (try: {title.Value})");
    }
}
```

## Rules for LLMs / Agents

- When a validation/business rule might grow, combine with others, or depend on runtime context, define it as an interface implementation (`IsSatisfiedBy` / apply-or-correct method), not an inline `if` in the consuming code.
- Combine multiple rule implementations behind a composite that itself implements the same rule interface (`AllTitleValidity : ITitleValidity`), so consumers always depend on exactly one rule instance regardless of how many concrete rules are active.
- Register/inject the composed rule as a dependency (constructor or handler parameter) rather than `new`-ing up rule instances inside the method that uses them — this is what allows swapping, testing, and per-tenant/per-role configuration of the active rule set.
- Let rules read ambient context (current user, configuration) through constructor-injected dependencies (`Func<ClaimsPrincipal> getUser`) rather than the calling code passing that context down manually into a validation function.
- Keep each concrete rule small and single-purpose (one class per rule); do not let one rule implementation grow to check multiple unrelated conditions — compose via `AllXValidity` instead.

## When NOT to apply

For a single, truly fixed, never-varying validation (a required-field check that will never depend on context or be combined with anything else), an inline check is simpler and the interface/composite machinery is unnecessary overhead. Introduce the pattern once a second rule, a context-dependent rule, or configurable rule sets are anticipated.

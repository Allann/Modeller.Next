---
title: "Clean Architecture Use Cases"
---

# Clean Architecture Use Cases


## The Standard

Application behavior MUST be organized as single-purpose use-case classes (e.g. `RegisterUser`) that depend only on narrow, domain-owned interfaces (e.g. `IUserRepository`, `IPasswordHasher`) — never on concrete infrastructure (`DbContext`, HTTP clients, etc.) directly — so the use case can be unit tested with substitutes/mocks and has no knowledge of persistence or transport details.

## Before (Anti-pattern)

No "before" state exists for this topic: `Web.Api` in the `before` folder is an empty API skeleton with no domain code, no use cases, and no tests — it demonstrates the starting point (a bare ASP.NET Core project) rather than a naive anti-pattern to contrast against.

## Why

The "after" sample adds a `Users` feature folder containing: a plain `User` entity, an `IUserRepository` interface (`Insert`, `Exists`) and an `IPasswordHasher` interface owned by the application/domain layer, and a `RegisterUser` use case that takes both interfaces as constructor dependencies and expresses the entire registration workflow (check uniqueness, hash password, build entity, persist, return) with no reference to EF Core, SQL, or HTTP. Because `RegisterUser` depends only on interfaces, `RegisterUserTests` can substitute both dependencies with `NSubstitute` and verify the use case's behavior — successful registration inserts the user, duplicate email throws and skips the insert — without a database, web server, or any I/O. This is the core Clean Architecture payoff: business logic is tested and reasoned about independently of infrastructure choices, and infrastructure (the real EF Core repository, real password hasher) can be swapped without touching the use case.

## After (Standard)

```csharp
public interface IUserRepository
{
    Task Insert(User user);
    Task<bool> Exists(string email);
}

public sealed class RegisterUser(IUserRepository userRepository, IPasswordHasher passwordHasher)
{
    public record Request(string Email, string FirstName, string LastName, string Password);

    public async Task<User> Handle(Request request)
    {
        if (await userRepository.Exists(request.Email))
        {
            throw new Exception("The email is already in use");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PasswordHash = passwordHasher.Hash(request.Password)
        };

        await userRepository.Insert(user);

        return user;
    }
}
```

```csharp
// Unit test: no database, no web host - pure substitutes.
_userRepository.Exists(request.Email).Returns(false);
_passwordHasher.Hash(request.Password).Returns("hashed_password");

User user = await _handler.Handle(request);

await _userRepository.Received(1).Insert(user);
```

## Rules for LLMs / Agents

- Model each application behavior as a single-purpose use-case class with one public `Handle` (or equivalent) method and a `Request`/`Command` record describing its input.
- Use-case classes MUST depend only on interfaces (`IUserRepository`, `IPasswordHasher`, etc.), never on `DbContext`, `HttpClient`, or other concrete infrastructure types.
- Interfaces consumed by use cases belong to the application/domain layer (defined alongside the use case), with infrastructure providing the implementation — dependency direction points inward.
- Every use case MUST have unit tests that substitute its dependencies (e.g. via NSubstitute) and assert both the success path and the failure/edge-case path, without touching a real database or network.
- Keep use-case classes free of transport concerns (HTTP status codes, routing) — that belongs in the endpoint/controller mapping layer, not the use case itself.

## When NOT to apply

Trivial CRUD operations with no business rules may not need a dedicated use-case class if the project has already standardized on a lighter pattern (e.g. minimal generic repository calls) — but any operation with a business rule (uniqueness checks, calculations, side effects like email verification) should follow this pattern.

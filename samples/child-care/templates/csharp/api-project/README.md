# C# api-project pack

This reusable pack projects a complete canonical context into a compiling
ASP.NET Core Minimal API host. It exercises every scope the generation
engine supports and produces a materially deeper output tree than
`domain-project/`, which makes it useful for realistic generation testing:

- **context** (once): solution, csproj, `Program.cs`, global usings,
  `appsettings.json`, and the `IEndpointModule` marker interface that
  `Program.cs` discovers via reflection at startup — no per-entity wiring is
  hand-written, so the project-level templates stay entity-agnostic.
- **entity** (per Entity): a record with a synthetic `Id`, an
  interface-plus-in-memory repository pair, `Create`/`Update`/`Delete`
  command handlers, `Get`/`List` query handlers, and a Minimal API endpoint
  module exposing CRUD over `/api/{entity}`.
- **enumeration** (per Enumeration): a plain C# enum.
- **rule** (per Rule): the facts record and evaluator (as in
  `domain-project/`), plus an endpoint module that evaluates the rule over
  posted facts at `/api/rules/{rule}`.
- **behaviour** (per Behaviour): the lifecycle-stage enum and transition
  function (as in `domain-project/`), plus an endpoint module that applies
  the transition at `/api/behaviours/{behaviour}`.

Repositories, command/query handlers, and endpoint modules are registered in
`Program.cs` purely by reflecting over the compiled assembly for naming
conventions (`I*Repository`, `*Handler`, `IEndpointModule` implementations),
since project-level (`context` scope) templates never see the list of
entities, rules, or behaviours in the context — only per-definition templates
do. None of the output recipes name a Child Care definition.

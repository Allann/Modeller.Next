# QA procedure: Read-only generation preview

Proves that a caller can ask the hosted workspace API to plan and render a
template pack's artifacts for a draft workspace, entirely read-only, and get
back the same kind of structured, diagnostics-first response that workspace
analysis already returns — never an unhandled error, and never a write to a
filesystem.

Run this against a running `Modeller.Api` instance (locally or the deployed
playground API), sending JSON requests the same way `analyze` and `export`
are exercised today.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- `Modeller.Api` running locally (`dotnet run --project src/Modeller.Api`) or
  a reachable deployment.
- A JSON HTTP client (curl, Postman, or the browser devtools console).

## Steps

### Part 1 — a valid draft produces a preview

1. Send a generation-preview request naming:
   - one or more RML documents declaring a bounded context with at least one
     entity;
   - an ephemeral identity;
   - workspace configuration (generation contract version, logical output
     root);
   - the known template pack's ID.
2. Confirm the response reports no diagnostics.
3. Confirm the response lists one or more proposed artifacts, ordered
   consistently between repeated identical requests.
4. Confirm every listed artifact carries: its output path, its owning
   definition (or "no owner" for a project-level artifact), the template
   pack's ID, and the template's ID.
5. Confirm every listed artifact carries its fully rendered text content.
6. Confirm no file appears anywhere on the server's filesystem as a result of
   this request (there is no "download" or "apply" step to run — the request
   itself is the whole interaction).

### Part 2 — repeating the same request is deterministic

1. Send the exact same request from Part 1 a second time.
2. Confirm the second response lists the same artifacts, in the same order,
   with byte-identical rendered content to the first response.

### Part 3 — a draft that fails to parse still returns a structured response

1. Send a generation-preview request whose document text contains a syntax
   error, naming the known template pack.
2. Confirm the response is not an unhandled error (no 5xx, no empty body).
3. Confirm the response reports diagnostics explaining the draft could not be
   parsed.
4. Confirm the response's artifact list is empty.

### Part 4 — a draft that fails validation still returns a structured response

1. Send a generation-preview request for a draft that parses but fails
   semantic validation (for example, a field with an invalid type), naming
   the known template pack.
2. Confirm the response reports diagnostics explaining the validation
   failure.
3. Confirm the response's artifact list is empty.

### Part 5 — an unrecognized template pack is a diagnostic, not an error

1. Send a generation-preview request for an otherwise-valid draft, naming a
   template pack ID the server does not recognize (for example, a made-up
   ID).
2. Confirm the response reports a diagnostic explaining the template pack is
   unknown.
3. Confirm the response's artifact list is empty.
4. Confirm this is not a 400 or 500 — an unrecognized pack ID is expected
   user input, handled the same way an invalid draft is.

### Part 6 — an incompatible generation contract is a diagnostic, not an error

1. Send a generation-preview request for an otherwise-valid draft, declaring
   a generation contract version that does not match the known template
   pack's declared contract version.
2. Confirm the response reports a diagnostic explaining the generation
   contract is incompatible.
3. Confirm the response's artifact list is empty.

### Part 7 — a request that violates the API's own shape limits is rejected outright

1. Send a generation-preview request declaring more documents than the
   documented per-request document limit (the same limit `analyze` already
   enforces).
2. Confirm the response is rejected with a structured "malformed request"
   diagnostic before any parsing of the draft's content is attempted.
3. Confirm this response is distinguishable from Part 3/Part 4/Part 5's
   content-level diagnostics (it reports a request-shape problem, not a
   problem with the draft itself).

### Part 8 — the preview is stateless across requests

1. Send a generation-preview request for one draft (Draft A).
2. Send a second, unrelated generation-preview request for a different draft
   (Draft B) that happens to reuse the same document paths as Draft A but
   different content.
3. Confirm Draft B's response reflects only Draft B's content — nothing from
   Draft A's request carries over.

## Pass criteria

- Part 1 and Part 2 together show a working, deterministic preview: real
  rendered content, stable ordering, no filesystem write.
- Part 3, Part 4, Part 5, and Part 6 each return a normal (non-error)
  response whose diagnostics explain what went wrong, with an empty artifact
  list — never an unhandled exception or empty body.
- Part 7 shows the request-shape limits already enforced on `analyze` apply
  here too, and are checked before the draft's content is ever examined.
- Part 8 confirms no server-side session or cross-request state exists.

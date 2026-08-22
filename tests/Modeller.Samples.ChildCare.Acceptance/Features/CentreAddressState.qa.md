# QA procedure: A centre address records its state from a shared state list

Proves that `samples/child-care` can relate a centre address to a shared
State entity in place of a free-text field, as a deterministic addition to
the sample. This State entity is designed to be reused by School in a
later increment, so give it a stable, general shape now (a code and a
name) rather than one scoped only to addresses.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. **Confirm the current gap.**
   Open `samples/child-care/model/entities/centre-address.modeller`.
   Confirm "State" is a plain string field.

2. **Add the state entity.**
   Add a new entity file (for example
   `samples/child-care/model/entities/state.modeller`) with a state code
   field (short, unique) and a state name field, matching the legacy
   `State` domain's shape.

3. **Replace the field with a relationship.**
   In `centre-address.modeller`, replace the "State" field with a required
   "State" relationship targeting the new entity. Remove the file's leading
   comment about the simplification once it no longer applies.

4. **Declare the new source and mint identities.**
   Add the new entity file to `samples/child-care/.modeller/config.json`.
   Follow `docs/getting-started/create-definition.md` to mint UUIDv7
   identities for the new entity and the changed relationship line, in
   document order.

5. **Preview the change.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```
   Confirm the preview lists only the expected files as changed, and every
   other file as unchanged. Confirm the command exits successfully.

6. **Generate for real.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
   ```
   Confirm it exits successfully. Open Centre address's generated file and
   confirm State is now a relationship to the new State entity, not a
   string field.

7. **Confirm idempotence.**
   Run the same `generate` command a second time. Confirm every file is
   reported as `Unchanged`.

8. **Confirm the projections still work.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
   ```
   Confirm Centre address shows an edge to the new State entity.

9. **Update the sample's own documentation.**
   In `samples/child-care/gaps.md`, remove the "Centre address" bullet.

## Pass criteria

- Steps 5–7 all succeed with no errors, and step 7's second generation
  reports no changes.
- A centre address's state reads as a relationship to a distinct State
  entity, not free text.
- Two addresses with different states each resolve to their own, correct
  state.
- `gaps.md` no longer lists this as an open gap.

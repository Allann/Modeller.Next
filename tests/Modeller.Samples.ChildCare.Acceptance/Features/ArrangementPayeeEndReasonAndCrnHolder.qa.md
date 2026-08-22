# QA procedure: An arrangement records its payee, end reason, and CRN holder

Proves that `samples/child-care` can relate an arrangement to its payee
account, an end reason, and a CRN-holding adult, as a deterministic
addition to the sample.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. **Confirm the current gap.**
   Open `samples/child-care/model/entities/arrangement.modeller`. Confirm
   Arrangement has no payee, end-reason, or CRN-holder relationship, and
   its leading comment names all three as deferred.

2. **Add the payee relationship.**
   Add a required "Payee" relationship on Arrangement targeting the
   existing "Account" entity (`account.modeller`).

3. **Add the end-reason entity and relationship.**
   Add a new entity file (for example
   `samples/child-care/model/entities/arrangement-end-reason.modeller`)
   describing an arrangement end reason: a short description field,
   matching the shape of `absence-reason.modeller`. Add an optional "End
   reason" relationship on Arrangement targeting it.

4. **Add the CRN-holder relationship.**
   Add an optional "CRN holder" relationship on Arrangement targeting the
   existing "Adult" entity (`reference-stubs.modeller`).

5. **Remove the now-stale comment.**
   Remove the "Payee/end-reason/CRN-holder relationships are added in a
   later increment" comment from the top of `arrangement.modeller`.

6. **Declare the new source and mint identities.**
   Add the new end-reason entity file to
   `samples/child-care/.modeller/config.json`. Follow
   `docs/getting-started/create-definition.md` to mint UUIDv7 identities for
   the new entity and for each new relationship line, in document order.

7. **Preview the change.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```
   Confirm the preview lists only the expected files as changed, and every
   other file as unchanged. Confirm the command exits successfully.

8. **Generate for real.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
   ```
   Confirm it exits successfully. Open Arrangement's generated file and
   confirm it exposes a required payee relationship to Account, an optional
   end-reason relationship, and an optional CRN-holder relationship to
   Adult.

9. **Confirm idempotence.**
   Run the same `generate` command a second time. Confirm every file is
   reported as `Unchanged`.

10. **Confirm the projections still work.**
    Run:
    ```
    dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
    ```
    Confirm Arrangement shows edges to Account, the new end-reason entity,
    and Adult.

11. **Update the sample's own documentation.**
    In `samples/child-care/gaps.md`, remove the Arrangement bullet (or
    amend it if any part is intentionally still deferred). Update
    `samples/child-care/README.md`'s "Current slice" section to mention the
    arrangement's payee, end reason, and CRN holder.

## Pass criteria

- Steps 7–9 all succeed with no errors, and step 9's second generation
  reports no changes.
- An arrangement can be authored with a payee, an end reason, and a CRN
  holder, each reading as a distinct relationship.
- An arrangement with a payee but no end reason and no CRN holder still
  compiles (both are optional).
- `gaps.md` and `README.md` no longer describe this as an open gap.

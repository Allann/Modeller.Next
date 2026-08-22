# QA procedure: A child records community support and specialised support required

Proves that `samples/child-care` can relate a child to the community
support programs it draws on and the specialised support it requires, as a
deterministic addition to the sample.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. **Confirm the current gap.**
   Open `samples/child-care/model/entities/child.modeller`. Confirm there
   is no community-support or specialised-support-required relationship.

2. **Add the two reference entities.**
   Add two new entity files, each with a short description field, matching
   the shape of `absence-reason.modeller`: one for community support
   (legacy `ChildCommunitySupport`), one for specialised support required
   (legacy `ChildAdditionalNeedsSpecialisedSupportRequired`). Keep them
   separate entities — the legacy model deliberately keeps additional-needs
   support distinct from medical-condition support, even though both stay
   out of scope for medical records specifically.

3. **Add the relationships to Child.**
   In `child.modeller`, add two "many" relationships: "Community support"
   targeting the first new entity, and "Support required" targeting the
   second. Both are optional (zero-or-many).

4. **Declare the new sources and mint identities.**
   Add both new entity files to
   `samples/child-care/.modeller/config.json`. Follow
   `docs/getting-started/create-definition.md` to mint UUIDv7 identities
   for the new entities and relationships, in document order.

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
   Confirm it exits successfully. Open Child's generated file and confirm
   it now exposes both relationships as collections.

7. **Confirm idempotence.**
   Run the same `generate` command a second time. Confirm every file is
   reported as `Unchanged`.

8. **Confirm the projections still work.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
   ```
   Confirm Child shows edges to both new entities.

9. **Update the sample's own documentation.**
   In `samples/child-care/gaps.md`, remove "community support" and
   "additional-needs" from the Child bullet's list of missing
   relationships, leaving "medical records" and "consent" listed (those
   remain out of scope — see the untouched-capability-areas note).

## Pass criteria

- Steps 5–7 all succeed with no errors, and step 7's second generation
  reports no changes.
- A child can be authored with community support entries and specialised
  support required entries, each a distinct relationship.
- A child with neither still compiles (both relationships are optional).
- `gaps.md` reflects that only medical records and consent remain
  deferred for Child.

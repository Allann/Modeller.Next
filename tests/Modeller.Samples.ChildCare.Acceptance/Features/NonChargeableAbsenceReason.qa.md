# QA procedure: A non-chargeable absence records its own reason

Proves that `samples/child-care` can record a distinct reason for *why* an
absence was made non-chargeable, separate from the absence's general reason,
and that the change is a clean, deterministic addition to the sample.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. **Confirm the current gap.**
   Open `samples/child-care/model/entities/absence.modeller` and
   `samples/child-care/model/entities/absence-reason.modeller`. Confirm there
   is one "Absence reason" relationship on Absence and no separate
   non-chargeable reason.

2. **Add the non-chargeable reason entity.**
   Add a new entity file (for example
   `samples/child-care/model/entities/non-chargeable-reason.modeller`)
   describing a non-chargeable reason: a short description field, matching
   the shape of the existing "Absence reason" entity.

3. **Add the relationship to Absence.**
   In `samples/child-care/model/entities/absence.modeller`, add an optional
   "Non chargeable reason" relationship to the new entity, alongside the
   existing "Non chargeable" flag and "Absence reason" relationship.

4. **Declare the new source and mint an identity.**
   Add the new entity file to `samples/child-care/.modeller/config.json`.
   Follow `docs/getting-started/create-definition.md` to mint a new UUIDv7
   identity for the new entity (and for the new relationship line) in
   `samples/child-care/.modeller/identities.json`, in document order.

5. **Preview the change.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```
   Confirm the preview lists only the expected files as changed (the new
   entity's generated output, and Absence's generated output), and every
   other file as unchanged. Confirm the command exits successfully with no
   errors.

6. **Generate for real.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
   ```
   Confirm it exits successfully. Open the newly generated file for the
   non-chargeable reason entity and confirm it has the expected fields. Open
   Absence's generated file and confirm it now exposes the non-chargeable
   reason relationship alongside the existing absence reason relationship,
   as two distinct properties.

7. **Confirm idempotence.**
   Run the same `generate` command a second time. Confirm every file is
   reported as `Unchanged` — nothing is rewritten just from running
   generation twice.

8. **Confirm the projections still work.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
   ```
   Confirm Absence appears with both an absence-reason and a
   non-chargeable-reason relationship, as two separate edges to two
   separate entities.

9. **Update the sample's own documentation.**
   In `samples/child-care/gaps.md`, remove or amend the "Absence" bullet so
   it no longer lists the non-chargeable-reason conflation as a gap. In
   `samples/child-care/README.md`, update the "Current slice" section so it
   mentions the non-chargeable reason where it currently just says
   "Determine absence chargeability Rule and Record absence Behaviour".

## Pass criteria

- Steps 5–7 all succeed with no errors, and step 7's second generation
  reports no changes.
- A non-chargeable absence can carry a non-chargeable reason that reads
  differently from, and independently of, its general absence reason.
- An absence that is not marked non-chargeable still compiles with no
  non-chargeable reason present (the relationship is optional; nothing
  existing breaks).
- `gaps.md` and `README.md` no longer describe this as an open gap.

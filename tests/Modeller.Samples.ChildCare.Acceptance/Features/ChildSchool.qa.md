# QA procedure: A school-aged child records their school

Proves that `samples/child-care` can relate a child to the school they
attend, as a deterministic addition to the sample.

**Depends on**: the State entity added by the centre-address state
increment (`CentreAddressState.qa.md`). Do this increment after that one,
so School can reuse the same State entity rather than inventing a second
one.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.
- The State entity from `CentreAddressState.qa.md` already exists in
  `samples/child-care/model/entities/state.modeller`.

## Steps

1. **Confirm the current gap.**
   Open `samples/child-care/model/entities/child.modeller`. Confirm there
   is no school relationship, classroom field, or school-start-year field.

2. **Add the school type entity.**
   Add a new entity file for a school type (a short description field),
   matching the legacy `SchoolType` domain.

3. **Add the school entity.**
   Add a new entity file for a school: name, suburb, postcode fields; a
   required "State" relationship to the existing State entity; a required
   "School type" relationship to the new school-type entity; and a
   "Location" field using the `coordinate` data type (a single field
   replaces the legacy's separate Latitude/Longitude pair, matching the
   choice made for Centre).

4. **Add the relationship and fields to Child.**
   In `child.modeller`, add an optional "School" relationship targeting
   the new school entity, an optional "Classroom" string field, and an
   optional "School start year" integer field.

5. **Declare the new sources and mint identities.**
   Add both new entity files to
   `samples/child-care/.modeller/config.json`. Follow
   `docs/getting-started/create-definition.md` to mint UUIDv7 identities
   for the new entities and fields, in document order.

6. **Preview the change.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```
   Confirm the preview lists only the expected files as changed, and every
   other file as unchanged. Confirm the command exits successfully.

7. **Generate for real.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
   ```
   Confirm it exits successfully. Open Child's generated file and confirm
   it now exposes the school relationship, classroom, and school-start-year
   as optional properties.

8. **Confirm idempotence.**
   Run the same `generate` command a second time. Confirm every file is
   reported as `Unchanged`.

9. **Confirm the projections still work.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
   ```
   Confirm Child shows an edge to the new School entity, and School shows
   edges to State and School type.

10. **Update the sample's own documentation.**
    In `samples/child-care/gaps.md`, remove "school" from the Child
    bullet's list of missing relationships.

## Pass criteria

- Steps 6–8 all succeed with no errors, and step 8's second generation
  reports no changes.
- A school-aged child can be authored with a school, classroom, and start
  year.
- A child who is not school-aged still compiles with no school recorded
  (the relationship is optional).
- `gaps.md` no longer lists school as missing from Child.

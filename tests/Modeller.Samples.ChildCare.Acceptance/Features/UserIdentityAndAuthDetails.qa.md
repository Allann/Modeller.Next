# QA procedure: A user has real identity and authentication details

Proves that `samples/child-care` gives User real identity and
authentication fields in place of its current empty stub, as a
deterministic addition to the sample.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. **Confirm the current gap.**
   Open `samples/child-care/model/entities/reference-stubs.modeller`.
   Confirm `entity User` has no fields.

2. **Move User into its own file with real fields.**
   Move the User declaration out of `reference-stubs.modeller` into a new
   `samples/child-care/model/entities/user.modeller`, and give it: user
   name, first name, last name, authentication source system,
   authentication source tenant identifier, and authentication user
   identifier — matching the corresponding fields on the legacy `User`
   entity. Leave out the Organisations relationship — Organisation is not
   yet part of this sample.

3. **Retire `reference-stubs.modeller` if it is now empty.**
   If Adult has already been moved out in a prior increment and User is
   the last declaration in `reference-stubs.modeller`, delete the file and
   remove it from `.modeller/config.json`. If Adult has not yet been moved
   out, leave the file in place with just the Adult stub remaining.

4. **Declare the new source and mint identities.**
   Add the new entity file to `samples/child-care/.modeller/config.json`.
   Follow `docs/getting-started/create-definition.md` to mint UUIDv7
   identities for the new fields, in document order. Keep User's existing
   entity-level identity from `identities.json`.

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
   Confirm it exits successfully. Open User's generated file and confirm it
   now has the fields listed in step 2.

7. **Confirm idempotence.**
   Run the same `generate` command a second time. Confirm every file is
   reported as `Unchanged`.

8. **Confirm existing User references still resolve.**
   Confirm Absence's "Staff" relationship still compiles and still targets
   the (now fields-bearing) User entity without any change to
   `absence.modeller`.

9. **Confirm the projections still work.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
   ```
   Confirm User appears as a full entity node with the same relationships
   pointing to it as before.

10. **Update the sample's own documentation.**
    In `samples/child-care/gaps.md`, remove the "Adult, User" bullet
    entirely once both entities carry real fields (or update it to reflect
    only what is still deferred, if Adult has not yet been ported).

## Pass criteria

- Steps 5–7 all succeed with no errors, and step 7's second generation
  reports no changes.
- A user can be authored with a name, user name, and authentication
  source details.
- Absence's existing "Staff" relationship still resolves with no other
  file changed.
- `gaps.md` reflects that User is no longer an empty stub.

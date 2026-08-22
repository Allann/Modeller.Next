# QA procedure: An adult has real identity and contact details

Proves that `samples/child-care` gives Adult real identity and contact
fields in place of its current empty stub, as a deterministic addition to
the sample.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. **Confirm the current gap.**
   Open `samples/child-care/model/entities/reference-stubs.modeller`.
   Confirm `entity Adult` has no fields.

2. **Move Adult into its own file with real fields.**
   Move the Adult declaration out of `reference-stubs.modeller` into a new
   `samples/child-care/model/entities/adult.modeller`, and give it: first
   name, last name, former name (optional), date of birth (optional), CRN
   (optional), home phone (optional), mobile phone (optional), and email
   (optional) — matching the corresponding fields on the legacy `Adult`
   entity. Leave out Title, Gender, EthnicBackground, Languages, Addresses,
   EmploymentStatus, HighestEducationReceived, and CCSSConfirmedAdult —
   those need their own reference entities and stay out of scope for this
   change.

3. **Declare the new source and mint identities.**
   Add the new entity file to `samples/child-care/.modeller/config.json`
   and remove the moved declaration from `reference-stubs.modeller`'s
   config entry only if that file becomes empty of anything else (User
   still lives there). Follow
   `docs/getting-started/create-definition.md` to mint UUIDv7 identities
   for the fields, in document order. Because Adult already has a minted
   identity in `identities.json`, keep its existing entity-level identity —
   only new field lines need new identities.

4. **Preview the change.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```
   Confirm the preview lists only the expected files as changed — Adult's
   own generated output, plus any entity referencing Adult if regenerating
   references a changed type shape — and every other file as unchanged.
   Confirm the command exits successfully.

5. **Generate for real.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
   ```
   Confirm it exits successfully. Open Adult's generated file and confirm
   it now has the fields listed in step 2.

6. **Confirm idempotence.**
   Run the same `generate` command a second time. Confirm every file is
   reported as `Unchanged`.

7. **Confirm existing Adult references still resolve.**
   Confirm Absence's "Adult", "Confirmed by", and Arrangement's "CRN
   holder" relationships still compile and still target the (now
   fields-bearing) Adult entity without any change to those files.

8. **Confirm the projections still work.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
   ```
   Confirm Adult appears as a full entity node (not an empty stub) with
   the same relationships pointing to it as before.

9. **Update the sample's own documentation.**
   In `samples/child-care/gaps.md`, update the "Adult, User" bullet to
   describe only User as still an identity-only stub, and note which Adult
   relationships (title, gender, ethnic background, and so on) remain
   deferred to the untouched capability areas.

## Pass criteria

- Steps 4–6 all succeed with no errors, and step 6's second generation
  reports no changes.
- An adult can be authored with a name, date of birth, CRN, and contact
  details.
- An adult with only a name still compiles (all the added fields besides
  name are optional).
- Every existing relationship into Adult still resolves with no other file
  changed.
- `gaps.md` reflects that Adult is no longer an empty stub.

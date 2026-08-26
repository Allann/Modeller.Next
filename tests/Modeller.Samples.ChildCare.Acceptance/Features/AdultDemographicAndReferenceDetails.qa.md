# QA procedure: An adult records demographic and reference details

This procedure proves that the child-care sample extends Adult with reusable
demographic and reference details. It also proves that address type is a closed
set and that the new details remain optional.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. Review the Adult definition. Confirm it can identify one optional Title,
   one optional Gender, multiple Ethnic backgrounds, multiple Languages,
   multiple Adult addresses, multiple Adult employment statuses, one optional
   highest education received entry, and one optional CCSS-confirmed adult.

2. Review the reference definitions. Confirm Title, Gender, Ethnic background,
   Language, Adult employment status, and Adult highest education received are
   separate reusable entities with descriptions. Confirm these values are not
   string fields on Adult.

3. Review Adult address. Confirm it records address line 1, optional address
   line 2, suburb, postcode, one Address type, and one State. Confirm Address
   type uses the existing Residential, Commercial, and Postal members. Confirm
   State identifies the shared State entity rather than storing arbitrary text.

4. Review CCSS-confirmed adult. Confirm it records optional service identifier,
   CRN, and date of birth. Confirm Adult can identify one such record without
   copying those confirmed values into new Adult fields.

5. Review an Adult with no new details. Confirm all relationships added by
   this capability are optional and the Adult remains valid with its existing
   identity details only.

6. Review workspace configuration and identity metadata. Confirm each new
   source is included and every new semantic line has one stable UUIDv7
   identity. Confirm Adult keeps its existing identity.

7. Preview generation. Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```
   Confirm the command succeeds and lists only expected changed outputs.

8. Generate the workspace. Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
   ```
   Confirm the command succeeds. Review the generated Adult and supporting
   types and confirm they express the relationships and fields from steps 1–4.

9. Build the generated solution. Run:
   ```
   dotnet build samples/child-care/generated/ChildCare.slnx
   ```
   Confirm the build succeeds without errors.

10. Generate the workspace again. Confirm every output is reported as
    `Unchanged`.

11. Project the Structural view. Run:
    ```
    dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
    ```
    Confirm Adult has the new reference relationships, Adult address identifies
    State, and the supporting reference entities are distinct nodes.

12. Review the sample README and `gaps.md`. Confirm they describe this Adult
    capability as ported and do not claim that organisation membership, staff
    roles, security, notifications, or ownership were completed by this change.

## Pass criteria

- Adult can express all details named in steps 1–4 through typed relationships
  and the existing Address type enumeration.
- The reference entities can be reused by more than one Adult.
- An Adult without the new optional details remains valid.
- Preview, generation, generated-solution build, Structural projection, and
  second generation all succeed.
- The second generation reports every output as unchanged.
- The sample documentation records issue 130 as complete without absorbing
  work from issues 131–133.

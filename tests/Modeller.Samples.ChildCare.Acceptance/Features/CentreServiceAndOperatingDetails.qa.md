# QA procedure: A centre records its operations and organisational structure

Proves that the child-care sample records a centre's service offerings,
weekly operating hours, service care type, Australian Company Number,
coordinates, and place in the organisation. It also proves that rooms are
reached through the centre structure instead of a separate direct link.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. Review the centre in the sample. Confirm it records at least two service
   offerings, operating hours for at least one weekday, one service care
   type, an Australian Company Number, latitude, and longitude.

2. Review the service offering catalogue. Confirm each centre offering
   identifies one catalogue offering, and that each catalogue offering has
   a name and description.

3. Review the centre operating hours. Confirm each entry identifies a day,
   an opening time, and a closing time.

4. Review the organisation structure. Confirm a structure-node type states
   whether nodes of that type can contain centres. Confirm one child node
   refers to its parent and the centre belongs to a structure node.

5. Review the room connection. Confirm a room belongs to its centre and that
   the old separate direct Rooms relationship is absent from Centre.

6. Review the workspace sources and identities. Confirm each new definition
   is included in the workspace and each new semantic line has one stable
   UUIDv7 identity.

7. Preview generation.
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```
   Confirm the preview lists only the expected files as changed, and every
   other file as unchanged. Confirm the command exits successfully.

8. Generate the workspace.
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
   ```
   Confirm it exits successfully. Confirm the generated domain represents
   the operational details and structure reviewed in steps 1–5.

9. Build the generated solution. Confirm the build succeeds without errors.

10. Generate the workspace again. Confirm every output is unchanged.
   Run the same `generate` command a second time. Confirm every file is
   reported as `Unchanged`.

11. Project the Structural view.
    Run:
    ```
    dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
    ```
   Confirm the view shows the centre's service offerings, operating hours,
   and structure nodes. Confirm the room is connected through the centre
   structure and there is no separate direct Rooms relationship on Centre.

12. Review the sample README and gap list. Confirm they describe centre
    operations and structure as present and no longer describe direct Rooms
    or missing structure nodes as simplifications.

## Pass criteria

- Steps 7–11 succeed with no errors, and step 10's second generation
  reports no changes.
- A centre records catalogue-backed service offerings, operating hours, a
  service care type, an ACN, latitude, and longitude.
- A centre with no ACN still compiles (the field is optional).
- Parent and child structure nodes place a centre in the organisation.
- Centre exposes its structure nodes instead of a separate direct Rooms
  relationship. A room still identifies its centre.
- `gaps.md` and `README.md` reflect what is now covered.

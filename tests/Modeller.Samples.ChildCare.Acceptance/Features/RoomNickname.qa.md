# QA procedure: A room records a nickname and status history

Proves that the child-care sample records a room's optional nickname and
records its status with the reason, date, and optional notes for the change.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. Review a room in the sample. Confirm it can identify an optional nickname
   from the shared room-nickname list.

2. Review the room status. Confirm the room refers to a status record and
   does not hold only a status-type value.

3. Confirm the status record contains a room status type, a reason, a date,
   and optional notes.

4. Review the workspace sources and identities. Confirm the nickname and
   status definitions are included in the workspace and each new semantic
   line has one stable UUIDv7 identity.

5. Preview generation.
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```
   Confirm the preview lists only the expected files as changed, and every
   other file as unchanged. Confirm the command exits successfully.

6. Generate the workspace.
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
   ```
   Confirm it exits successfully. Confirm the generated room has an optional
   nickname relationship and a relationship to its status record.

7. Build the generated solution. Confirm the build succeeds without errors.

8. Generate the workspace again. Confirm every output is unchanged.
   Run the same `generate` command a second time. Confirm every file is
   reported as `Unchanged`.

9. Project the Structural view.
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
   ```
   Confirm Room appears with relationships to Room nickname and Room status.

10. Review the sample README and gap list. Confirm they no longer list Room
   nickname or audit-style Room status as deferred or simplified.

## Pass criteria

- Steps 5–9 succeed with no errors, and step 8's second generation
  reports no changes.
- A room can be authored with a nickname drawn from the shared nickname
  list.
- A room with no nickname still compiles (the relationship is optional).
- A room status records its type, reason, and date, with optional notes.
- `gaps.md` no longer lists the room nickname as missing.

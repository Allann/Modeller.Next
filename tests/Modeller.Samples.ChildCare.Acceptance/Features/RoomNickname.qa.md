# QA procedure: A room can carry a nickname alongside its number

Proves that `samples/child-care` can record a room's informal nickname,
drawn from a shared, reusable nickname list, as a deterministic addition to
the sample.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. **Confirm the current gap.**
   Open `samples/child-care/model/entities/room.modeller`. Confirm Room has
   no nickname relationship.

2. **Add the room nickname entity.**
   Add a new entity file (for example
   `samples/child-care/model/entities/room-nickname.modeller`) describing a
   room nickname: a short description field, matching the shape of
   `absence-reason.modeller`.

3. **Add the relationship to Room.**
   In `samples/child-care/model/entities/room.modeller`, add an optional
   "Room nickname" relationship to the new entity. Leave Room's `Status`
   field as-is — it stays a plain status field for this sample; do not
   introduce an audit-log-style status history as part of this change.

4. **Declare the new source and mint an identity.**
   Add the new entity file to `samples/child-care/.modeller/config.json`.
   Follow `docs/getting-started/create-definition.md` to mint UUIDv7
   identities for the new entity and relationship line in
   `samples/child-care/.modeller/identities.json`, in document order.

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
   Confirm it exits successfully. Open Room's generated file and confirm it
   now exposes the nickname relationship as an optional property.

7. **Confirm idempotence.**
   Run the same `generate` command a second time. Confirm every file is
   reported as `Unchanged`.

8. **Confirm the projections still work.**
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
   ```
   Confirm Room appears with a nickname relationship to a new nickname
   entity.

9. **Update the sample's own documentation.**
   In `samples/child-care/gaps.md`, remove the "RoomNickname is not ported"
   sentence from the Room bullet, keeping the note about Status staying a
   plain enumeration field (that part of the bullet is still accurate — it
   is a deliberate simplification, not a gap being closed here).

## Pass criteria

- Steps 5–7 all succeed with no errors, and step 7's second generation
  reports no changes.
- A room can be authored with a nickname drawn from the shared nickname
  list.
- A room with no nickname still compiles (the relationship is optional).
- `gaps.md` no longer lists the room nickname as missing.

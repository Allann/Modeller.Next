# QA procedure: A centre address records its state from a shared state list

Proves that the child-care sample relates a centre address to a shared State
entity instead of free text. The State has a stable code and name and can be
reused by other addresses.

## Preconditions

- A checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. Review the State list. Confirm Queensland has the code `QLD` and the name
   `Queensland`.

2. Review a centre address. Confirm its State identifies the shared State
   entry and is not free text.

3. Review two centre addresses in different states. Confirm each identifies
   its correct State entry.

4. Review the workspace sources and identities. Confirm the State definition
   is included in the workspace and each new semantic line has one stable
   UUIDv7 identity.

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
   Confirm it exits successfully. Open Centre address's generated file and
   confirm State is now a relationship to the new State entity, not a
   string field.

7. Build the generated solution. Confirm the build succeeds without errors.

8. Generate the workspace again. Confirm every output is unchanged.
   Run the same `generate` command a second time. Confirm every file is
   reported as `Unchanged`.

9. Project the Structural view.
   Run:
   ```
   dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
   ```
   Confirm Centre address shows an edge to the new State entity.

10. Review the sample README and gap list. Confirm the plain-State
   simplification is no longer listed.

## Pass criteria

- Steps 5–9 succeed with no errors, and step 8's second generation
  reports no changes.
- A centre address's state reads as a relationship to a distinct State
  entity, not free text.
- Two addresses with different states each resolve to their own, correct
  state.
- `gaps.md` no longer lists this as an open gap.

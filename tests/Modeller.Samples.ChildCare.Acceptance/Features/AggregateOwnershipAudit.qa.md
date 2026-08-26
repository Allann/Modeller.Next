# QA procedure: Aggregate ownership audit

This procedure proves that currently ported child-care entities preserve
supported aggregate ownership from the legacy `.key` files.

## Scope

The audit includes each current child-care entity that has a matching legacy
`.key` owner fact and whose owner entity is also ported in this sample. It does
not add entities only to satisfy owner facts, and it does not change legacy
facts for entities that remain out of scope.

## Steps

1. List the current child-care entity files.

2. Read the legacy `.key` files under
   `M:\Modeller\samples\child-care-old\entities`.

3. Match current entity names to legacy entity names, ignoring spaces and case.

4. For each matched current entity with a legacy owner fact, confirm that the
   owner entity is also present in the current sample.

5. Confirm that each supported match declares `owner "<Owner>"` in the current
   entity file.

6. Compile the workspace. Confirm that all owner references resolve.

7. Run the child-care acceptance tests. Confirm that the ownership audit passes
   deterministically.

8. Preview generation:

   ```text
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```

9. Generate the workspace:

   ```text
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
   ```

10. Build the generated solution.

11. Generate the workspace again. Confirm that every output is `Unchanged`.

12. Confirm that `gaps.md` no longer says the ported entities have not been
    audited for ownership.

## Pass criteria

- Every supported legacy owner fact for a currently ported entity is declared.
- Owner references resolve during compilation.
- Generation and the generated build succeed.
- The second generation reports every output as unchanged.

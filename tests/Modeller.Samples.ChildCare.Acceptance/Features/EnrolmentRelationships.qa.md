# QA procedure: An enrolment connects a child to care arrangements at a centre

This procedure proves that the Child Care sample contains one bounded
Enrolment capability based on the legacy domain evidence.

## Preconditions

- Use a checkout of this repository with the .NET SDK installed.
- Run all commands from the repository root.

## Steps

1. Open the Child Care sample model. Confirm that it contains an Enrolment
   entity and an Enrolment tag entity.

2. Confirm that an Enrolment identifies one Child and groups its
   Arrangements and Enrolment tags.

3. Confirm that Centre owns Enrolment. Confirm that the Enrolment identity
   and each new field or relationship identity are UUIDv7 values in the
   identity registry.

4. Follow one Enrolment to an Arrangement and then to the Arrangement's
   payee Account. Confirm that this path uses the existing Arrangement and
   Account relationships.

5. Confirm that the Enrolment capability does not add the wider Family or
   Related adult graph. That graph is tracked by issue 128.

6. Confirm that every new model source is declared in the Child Care
   workspace configuration.

7. Preview generation:

   ```powershell
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```

   Confirm that the command succeeds and reports only the expected
   Enrolment-related changes.

8. Generate the workspace:

   ```powershell
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
   ```

   Confirm that the command succeeds. Confirm that the generated Enrolment
   and Enrolment tag types contain the declared domain fields and
   relationships.

9. Build the generated solution:

   ```powershell
   dotnet build samples/child-care/generated/api/ChildCare.slnx
   ```

   Confirm that the build succeeds without errors.

10. Generate the workspace again with the command from step 8. Confirm that
    every generated output is reported as `Unchanged`.

11. Project the structural view:

    ```powershell
    dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
    ```

    Confirm that Enrolment connects to Child, Arrangement, and Enrolment
    tag, and that Arrangement connects to its payee Account.

12. Open `samples/child-care/README.md` and
    `samples/child-care/gaps.md`. Confirm that the README names the bounded
    Enrolment capability and that `gaps.md` no longer tracks Enrolment as
    wholly absent. Confirm that any deferred Family relationship points to
    issue 128.

## Pass criteria

- Enrolment identifies its Child and is owned by Centre.
- Enrolment groups its Arrangements and Enrolment tags.
- Each Arrangement keeps its path to the payee Account.
- New sources and UUIDv7 identities are present.
- Dry-run, full generation, generated-solution build, and structural
  projection succeed.
- A second full generation reports every output as `Unchanged`.
- The README and gap register describe the implemented and deferred scope.

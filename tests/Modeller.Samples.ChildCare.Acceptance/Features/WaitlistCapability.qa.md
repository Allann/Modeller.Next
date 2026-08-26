# QA procedure: A centre records a child's requested pattern of care

This procedure proves that the Child Care sample contains one bounded Waitlist
capability based on the legacy domain evidence.

## Preconditions

- Use a checkout of this repository with the .NET SDK installed.
- Run all commands from the repository root.

## Steps

1. Open the Child Care sample model. Confirm that it contains Waitlist,
   Waitlist day, and Waitlist end reason entities, and a Waitlist preference
   type enumeration.

2. Confirm that Centre owns Waitlist and that each Waitlist identifies one
   Child.

3. Confirm that Waitlist records a cycle week number, creation date, preferred
   start date, and optional preferred end date.

4. Confirm that Waitlist can identify one optional Room and one optional
   Waitlist end reason.

5. Confirm that Waitlist groups one or more Waitlist days and owns them. Confirm
   that each Waitlist day records a weekday and either Required or Flexible as
   its preference.

6. Confirm that Waitlist has no direct relationship to Booking or Session. A
   Waitlist expresses requested care; it does not create booked care.

7. Confirm that the capability does not add Family or Related adult concepts.
   Those concepts are tracked by issue 128.

8. Confirm that every new model source is declared in the Child Care workspace
   configuration. Confirm that each new entity, field, relationship,
   enumeration, and enumeration member identity is a UUIDv7 value in the
   identity registry.

9. Preview generation:

   ```powershell
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```

   Confirm that the command succeeds and reports only the expected changes.

10. Generate the workspace:

    ```powershell
    dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
    ```

    Confirm that the command succeeds. Confirm that the generated Waitlist,
    Waitlist day, and Waitlist end reason types contain the declared domain
    fields and relationships, and that the preference values are Required and
    Flexible.

11. Build the generated solution:

    ```powershell
    dotnet build samples/child-care/generated/api/ChildCare.slnx
    ```

    Confirm that the build succeeds without errors.

12. Generate the workspace again with the command from step 10. Confirm that
    every generated output is reported as `Unchanged`.

13. Project the structural view:

    ```powershell
    dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
    ```

    Confirm that Waitlist connects to Child, Waitlist day, optional Room, and
    optional Waitlist end reason. Confirm that the projection does not show a
    direct Waitlist connection to Booking or Session.

14. Open `samples/child-care/README.md` and
    `samples/child-care/gaps.md`. Confirm that the README names the bounded
    Waitlist capability and that `gaps.md` no longer tracks Waitlist as absent.
    Confirm that the deferred Family relationship still points to issue 128.

## Pass criteria

- A Centre-owned Waitlist identifies one Child.
- Waitlist records its fortnightly cycle and requested care period.
- Waitlist groups owned days with Required or Flexible preferences.
- Room, preferred end date, and end reason are optional.
- Waitlist does not create or directly reference a Booking or Session.
- Family and Related adult concepts remain deferred to issue 128.
- New sources and UUIDv7 identities are present.
- Dry-run, full generation, generated-solution build, and structural projection
  succeed.
- A second full generation reports every output as `Unchanged`.
- The README and gap register describe the implemented and deferred scope.

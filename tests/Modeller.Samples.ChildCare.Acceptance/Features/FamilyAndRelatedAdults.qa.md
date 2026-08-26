# QA procedure: A family connects children, related adults, accounts, and enrolments

This procedure proves that the Child Care sample preserves the separate
meanings of Adult, Related adult, Family, and Family account while connecting
the completed Enrolment boundary to the family graph.

## Preconditions

- Use a checkout of this repository with the .NET SDK installed.
- Run all commands from the repository root.

## Steps

1. Open the Child Care sample model. Confirm that it contains separate Family,
   Related adult, Family account, and Family account holder entities. Confirm
   that Adult remains the existing person entity.

2. Confirm that Family has an optional family name, groups its Children and
   Related adults, and identifies one Family account. Confirm that its optional
   pathway to centre and referral source each have a description rather than
   being an identity-only placeholder.

3. Confirm that Related adult identifies one Adult, records a display priority,
   identifies one relationship type, and groups its authorisations. Confirm
   that relationship type and authorisation each have a description.

4. Confirm that Family account identifies its general Account and groups
   Family account holders. Confirm that each holder identifies one Adult and
   has an account-holder rank. Confirm that financial responsibility does not
   automatically make an Adult a Related adult.

5. Confirm that Enrolment identifies one Family as well as its existing Child,
   Centre ownership, Arrangements, and tags. Follow the Family to the Child's
   Enrolment and then to an Arrangement.

6. Follow the Arrangement to its payee Account. Confirm that this remains the
   existing Arrangement-to-Account relationship. Confirm that Family account
   and Account remain separate concepts even when the Arrangement payee is the
   account used by that Family account.

7. Confirm that the capability adds no Adult demographic, address, employment,
   education, or government-confirmation details; no staff role or security
   model; no notification model; and no debt-collection or payment-plan model.

8. Confirm that every new model source is declared in the Child Care workspace
   configuration. Confirm that each new concept, field, and relationship has a
   UUIDv7 identity in the identity registry.

9. Preview generation:

   ```powershell
   dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
   ```

   Confirm that the command succeeds and reports only the expected changes.

10. Generate the workspace:

    ```powershell
    dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
    ```

    Confirm that the command succeeds and the generated types preserve all
    relationships and fields described in steps 1 through 6.

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

    Confirm the complete path Family to Child to Enrolment to Arrangement to
    Account. Also confirm the paths Family to Related adult to Adult and Family
    to Family account to Family account holder to Adult.

14. Open `samples/child-care/README.md` and `samples/child-care/gaps.md`.
    Confirm that the README names this bounded capability and that the gap
    register no longer tracks Family and RelatedAdult as absent. Confirm that
    deferred details remain assigned to their existing issues or are recorded
    as intentional simplifications.

## Pass criteria

- Adult, Related adult, Family, Family account, and Account remain distinct.
- Family groups Children and Related adults and owns one Family account.
- Related adult records one Adult's family-specific relationship, display
  priority, and authorisations.
- Ranked Family account holders connect financial responsibility to Adults.
- Enrolment connects its existing Child and Arrangements to one Family.
- Arrangement retains its direct payee Account relationship.
- New sources and UUIDv7 identities are present.
- Dry-run, full generation, generated-solution build, and structural projection
  succeed.
- A second full generation reports every output as `Unchanged`.
- The README and gap register describe the implemented and deferred scope.

# QA procedure: Report care and record government subsidy entitlements

This procedure proves one bounded government-subsidy workflow in the Child Care
sample. The workflow starts with a government-confirmed child and a care
arrangement. It ends when the returned weekly and per-session subsidy
entitlements are recorded.

## Preconditions

- Use a checkout of this repository with the .NET SDK installed.
- Run all commands from the repository root.

## Steps

1. Open the Child Care sample model. Confirm that it contains government-confirmed
   child details, a government enrolment occurrence, a weekly session report, a
   weekly subsidy result, and a session entitlement.

2. Confirm that a government enrolment occurrence belongs to one Arrangement.
   Confirm that it records a government enrolment identifier, occurrence number,
   government stage, government end date, and visible stage.

3. Confirm that government enrolment readiness requires confirmed child details.
   Confirm that the negative conclusion explains that confirmed details are
   missing.

4. Confirm that a weekly session report belongs to one Arrangement, records its
   week start date and reporting stages, and groups the delivered Bookings for
   that week.

5. Confirm that session-report readiness requires an active government enrolment
   occurrence and at least one delivered Booking. Confirm that each missing fact
   has a distinct finding.

6. Confirm that submitting a ready weekly session report changes its lifecycle
   from Draft to Submitted. Confirm that a rejected submission does not make that
   transition.

7. Confirm that a weekly subsidy result records the weekly fee, care hours,
   entitlement amount, subsidised hours, preschool subsidised hours, absence
   count, processing stage, and optional error details.

8. Confirm that the weekly subsidy result groups session entitlements. Confirm
   that each session entitlement identifies its Booking and records the processed
   time, charged amount, absence indicator, entitlement type, amount, subsidised
   hours, recipient, and optional nil-or-partial reason.

9. Follow an ACCS Arrangement through the existing ACCS eligibility and
   determination capability. Confirm that an eligible ACCS Arrangement uses the
   same enrolment-occurrence and weekly-reporting path. Confirm that this
   increment does not declare a second ACCS determination.

10. Confirm that payments, family and related-adult details, provider personnel,
    staff authorization, and notifications are not part of this workflow.

11. Confirm that every new model source is declared in the Child Care workspace
    configuration and that every new semantic identity is UUIDv7 in the identity
    registry.

12. Preview generation:

    ```powershell
    dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
    ```

    Confirm that the command succeeds and reports only the expected
    government-subsidy changes.

13. Generate the workspace:

    ```powershell
    dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
    ```

    Confirm that the command succeeds and that the generated types contain the
    declared fields, relationships, rules, lifecycles, and behaviours.

14. Build the generated solution:

    ```powershell
    dotnet build samples/child-care/generated/api/ChildCare.slnx
    ```

    Confirm that the build succeeds without errors.

15. Generate the workspace again with the command from step 13. Confirm that
    every generated output is reported as `Unchanged`.

16. Project the structural, lifecycle, rule-decision, and behaviour views.
    Confirm that they show the enrolment occurrence, weekly report, subsidy
    result, session entitlements, readiness rules, and report submission
    lifecycle without unrelated capability areas.

17. Open `samples/child-care/README.md` and `samples/child-care/gaps.md`.
    Confirm that they describe this bounded workflow, its reuse of the existing
    ACCS capability, and its deferred scope.

## Pass criteria

- A government enrolment occurrence connects confirmed child details to an
  Arrangement and exposes its government and visible stages.
- Readiness rules explain missing confirmed-child and active-occurrence facts.
- A ready weekly report groups delivered Bookings and moves from Draft to
  Submitted.
- Returned weekly results group complete per-session subsidy entitlements.
- Eligible ACCS Arrangements reuse this reporting path without duplicating ACCS
  determination.
- New sources and UUIDv7 identities are present.
- Dry-run, full generation, generated-solution build, and all four projections
  succeed.
- A second full generation reports every output as `Unchanged`.
- The README and gap register describe the implemented and deferred scope.

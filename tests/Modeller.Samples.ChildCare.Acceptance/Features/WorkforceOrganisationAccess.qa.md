# QA procedure: Workforce organisation access

This procedure proves that the child-care sample contains a bounded workforce
and access-control model. It also proves that access is denied unless a current,
organisation-consistent assignment grants the required right at the exact
structure node.

## Scope

The slice includes organisations, user memberships, employees, rights, rights
groups, roles, and time-bounded security assignments. An access decision uses
the requesting user, organisation, required right, exact structure node, and
decision date.

The slice does not include sign-in, credentials, role administration, inherited
access through the structure hierarchy, notifications, audit history, or the
personnel roles used for government reporting.

## Preconditions

- Use a checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.
- Use 26 August 2026 as the decision date where a step needs a date.

## Steps

1. Open the child-care sample model. Confirm that Organisation records its
   name, short name, and abbreviation. Confirm that User can identify more
   than one Organisation.

2. Confirm that Employee belongs to one Organisation and identifies one User.
   Confirm that Employee records an external employee identifier, name,
   occupation code, authentication subject identifier, optional government
   person identifier, optional hire date, and optional termination date.

3. Confirm that a named Right can belong to a Rights group, a named Role can
   contain rights groups, and each role belongs to one Organisation.

4. Confirm that a Security assignment belongs to one Organisation and
   identifies one User, one Role, and one Structure node. Confirm that it has
   a required effective start date and an optional effective end date.

5. Compile the workspace. Confirm that all new declarations and relationships
   resolve without an error.

6. Evaluate access for a user who is a member of Harbour Child Care. Give that
   user a current Educator assignment at Brisbane Centre. Give the Educator role
   the `attendance_read` right through an Attendance readers rights group.
   Confirm that `attendance_read` access at Brisbane Centre is allowed.

7. Repeat the decision with the required right changed to
   `attendance_change`. Confirm that access is denied and that no state changes.

8. Repeat the original decision for Gold Coast Centre. Confirm that access is
   denied. Do not treat a parent or child structure node as an implicit match.

9. Set the assignment start date to 1 September 2026. Decide access on
   26 August 2026. Confirm that access is denied.

10. Set the assignment start date to 1 August 2026 and its end date to
    25 August 2026. Decide access on 26 August 2026. Confirm that access is
    denied.

11. Remove Harbour Child Care from the user's memberships while the role and
    assignment stay in Harbour Child Care. Confirm that access is denied.

12. Try to combine a Harbour Child Care user and role with a River Child Care
    structure node in one security assignment. Confirm that the assignment is
    rejected as invalid and cannot become observable.

13. Run the child-care acceptance tests. Confirm that the allowed path, each
    denied path, and the cross-organisation invariant pass deterministically.

14. Preview generation:

    ```text
    dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
    ```

    Confirm that the command succeeds and lists only the expected changes.

15. Generate the workspace:

    ```text
    dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
    ```

    Confirm that generation succeeds. Build the generated solution and confirm
    that the build succeeds.

16. Run the same generation command again. Confirm that every output is
    reported as `Unchanged`.

17. Confirm that each new declaration and member has a durable UUIDv7 identity
    and that every new source is declared in the workspace configuration.

18. Confirm that the child-care README describes the bounded workforce model.
    Confirm that `gaps.md` no longer tracks organisation membership, staff
    roles, or security under issue 131 and still records the exclusions in this
    procedure.

## Pass criteria

- The sample compiles, generates, and builds without an error.
- A current exact-node assignment grants only rights held by its role.
- Missing rights, another structure node, a future assignment, an ended
  assignment, and missing organisation membership each deny access.
- A security assignment cannot combine facts from different organisations.
- The second generation reports every output as unchanged.
- Documentation and durable identities agree with the implemented boundary.

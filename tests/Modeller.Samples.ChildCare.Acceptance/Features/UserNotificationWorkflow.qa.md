# QA procedure: User notification workflow

This procedure proves that the child-care sample contains a bounded user
notification workflow based on the legacy user-notification evidence.

## Scope

The slice includes an organisation-owned User notification, user notification
status, user notification type, and one workflow that creates, views, and
completes a user-audience notification. The slice does not include centre or
provider audiences, delivery channels, retry queues, templates, external
notification providers, or read receipts.

## Preconditions

- Use a checkout of this repository with the .NET SDK installed.
- Run commands from the repository root.

## Steps

1. Open the child-care sample model. Confirm that User notification belongs to
   one Organisation and identifies one User.

2. Confirm that User notification records subject, description, and an optional
   URL.

3. Confirm that User notification has a required User notification type and a
   required User notification status.

4. Confirm that User notification type contains User, Centre, and Provider
   members.

5. Confirm that User notification status contains New, Viewed, and Completed
   members.

6. Compile the workspace. Confirm that the new declarations and relationships
   resolve without an error.

7. Create a user notification. Confirm that it is created with User audience
   type and New status.

8. View the notification. Confirm that its status becomes Viewed.

9. Complete the notification. Confirm that its status becomes Completed.

10. Try to treat the completed notification as new again. Confirm that it stays
    Completed.

11. Run the child-care acceptance tests. Confirm that the model and workflow
    scenarios pass deterministically.

12. Preview generation:

    ```text
    dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
    ```

    Confirm that the command succeeds and lists only the expected changes.

13. Generate the workspace:

    ```text
    dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
    ```

    Confirm that generation succeeds. Build the generated solution and confirm
    that the build succeeds.

14. Run the same generation command again. Confirm that every output is
    reported as `Unchanged`.

15. Confirm that each new declaration and member has a durable UUIDv7 identity
    and that every new source is declared in the workspace configuration.

16. Confirm that the child-care README describes the user-notification workflow.
    Confirm that `gaps.md` marks notifications as ported under issue 132 and
    records the exclusions in this procedure.

## Pass criteria

- The sample compiles, generates, and builds without an error.
- User notification has organisation ownership, a user relationship, content,
  type, status, and lifecycle state.
- Creating, viewing, and completing a user notification follow the bounded
  workflow.
- A completed notification cannot return to New.
- The second generation reports every output as unchanged.
- Documentation and durable identities agree with the implemented boundary.

Feature: User notifications are created and completed inside an organisation

  The child-care sample needs a bounded notification workflow for messages sent
  to a specific system user. A user notification belongs to one organisation,
  identifies one user, records a subject, description, optional URL, audience
  type, and current status. Creating a user notification starts it as New.
  Viewing and completing the notification move it through its lifecycle.

  This slice covers user-audience notifications only. Centre and provider
  notification audiences, message delivery channels, retry queues, templates,
  external notification providers, and read receipts are outside this feature.

  Background:
    Given the child-care sample workspace

  Scenario: A user notification records its organisation, user, content, type, and status
    Given the bounded user notification model has been added to the sample workspace
    When the workspace is compiled
    Then compilation succeeds
    And a user notification belongs to one organisation
    And a user notification identifies one user
    And a user notification records a subject, description, and optional URL
    And a user notification has a user notification type and status

  Scenario: A new user notification is created for one user
    Given the user notification workflow starts from a draft notification
    When a user notification is created
    Then the notification is New
    And the notification is for the User audience

  Scenario: A viewed notification can be completed
    Given a user notification is New
    When the user views the notification
    Then the notification is Viewed
    When the user completes the notification
    Then the notification is Completed

  Scenario: A completed notification does not return to New
    Given a user notification is Completed
    When the user tries to view it as a new notification again
    Then the notification stays Completed

  Scenario: Generating the user notification model a second time reports no changes
    Given the bounded user notification generation model has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

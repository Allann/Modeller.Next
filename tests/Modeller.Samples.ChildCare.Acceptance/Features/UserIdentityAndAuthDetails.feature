Feature: A user has real identity and authentication details

  User is referenced by Absence's "Staff" relationship, but today it is an
  empty identity-only stub. This gives it the core identity and
  authentication fields the legacy model already establishes, so a staff
  member in this sample is a real person tied to a real authentication
  identity, not a placeholder.

  Note: the user's organisation memberships stay out of scope for this
  change — Organisation is not yet part of this sample.

  Background:
    Given the child-care sample workspace

  Scenario: A user records their name and user name
    Given a user named "Alex Chen" with the user name "alex.chen"
    When the workspace is compiled
    Then compilation succeeds
    And the user's name is "Alex Chen"
    And the user's user name is "alex.chen"

  Scenario: A user records their authentication source
    Given a user authenticated by the source system "Entra ID" with tenant "child-care-tenant" and identifier "alex.chen@child-care.example"
    When the workspace is compiled
    Then compilation succeeds
    And the user's authentication source system is "Entra ID"
    And the user's authentication tenant is "child-care-tenant"
    And the user's authentication identifier is "alex.chen@child-care.example"

  Scenario: Generating the sample workspace a second time reports no changes
    Given the user "Alex Chen" has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

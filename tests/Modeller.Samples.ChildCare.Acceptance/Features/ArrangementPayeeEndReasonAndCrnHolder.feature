Feature: An arrangement records its payee, end reason, and CRN holder

  An arrangement drives which sessions a family books, but today it cannot
  say who pays for it, why it ended, or which adult holds the family's CRN
  (Customer Reference Number) for the Australian Government's Child Care
  Subsidy. All three are known in the legacy model and are needed to relate
  billing and subsidy processing back to the right people.

  Background:
    Given the child-care sample workspace

  Scenario: An arrangement records its payee
    Given an arrangement with the account "Smith family account" as its payee
    When the workspace is compiled
    Then compilation succeeds
    And the arrangement's payee is the account "Smith family account"

  Scenario: An ended arrangement records why it ended
    Given an arrangement that has ended, with the end reason "Family relocated"
    When the workspace is compiled
    Then compilation succeeds
    And the arrangement's end reason is "Family relocated"

  Scenario: An arrangement records its CRN holder
    Given an arrangement with the adult "Jane Smith" recorded as its CRN holder
    When the workspace is compiled
    Then compilation succeeds
    And the arrangement's CRN holder is the adult "Jane Smith"

  Scenario: An arrangement with no end reason or CRN holder still compiles unchanged
    Given an arrangement with a payee, no end reason, and no CRN holder
    When the workspace is compiled
    Then compilation succeeds
    And the arrangement has no end reason
    And the arrangement has no CRN holder

  Scenario: Generating the sample workspace a second time reports no changes
    Given the arrangement end reason "Family relocated" has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

Feature: A centre address records its state from a shared state list

  A centre address currently records its state as free text. Free text lets
  the same state be spelled or abbreviated differently across addresses,
  which breaks anything that needs to group or validate addresses by state.
  The state should instead be chosen from one shared, reusable list of
  states and territories.

  Background:
    Given the child-care sample workspace

  Scenario: A centre address records its state from the shared state list
    Given a centre address in the suburb "Fortitude Valley" with the state "Queensland"
    When the workspace is compiled
    Then compilation succeeds
    And the centre address's state is "Queensland"

  Scenario: Two centre addresses in different states both resolve to their own state
    Given a centre address with the state "Queensland" and another centre address with the state "New South Wales"
    When the workspace is compiled
    Then compilation succeeds
    And the first centre address's state is "Queensland"
    And the second centre address's state is "New South Wales"

  Scenario: Generating the sample workspace a second time reports no changes
    Given the state "Queensland" has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

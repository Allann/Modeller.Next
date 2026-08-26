Feature: An enrolment connects a child to care arrangements at a centre

  An enrolment records a child's connection to a centre. It groups the
  child's care arrangements, permits enrolment tags, and preserves the
  account that pays for each arrangement.

  The wider family and related-adult graph belongs to a separate capability.

  Background:
    Given the child-care sample workspace

  Scenario: An enrolment identifies its child and centre
    Given the child "Alex Smith" attends the centre "River Street"
    When the child's enrolment is recorded
    Then the enrolment is for the child "Alex Smith"
    And the enrolment is owned by the centre "River Street"

  Scenario: An enrolment groups the child's care arrangements
    Given the child "Alex Smith" has an enrolment at the centre "River Street"
    And the enrolment has the arrangements "Before school care" and "Vacation care"
    When the enrolment is reviewed
    Then both arrangements belong to that enrolment

  Scenario: An enrolment records its tags
    Given the child "Alex Smith" has an enrolment at the centre "River Street"
    And the enrolment has the tags "New starter" and "Transport required"
    When the enrolment is reviewed
    Then both tags describe that enrolment

  Scenario: An enrolment reaches each arrangement's payee account
    Given the child "Alex Smith" has an enrolment with the arrangement "Before school care"
    And the arrangement is paid by the account "Smith family account"
    When the enrolment's arrangements are reviewed
    Then "Before school care" is paid by the account "Smith family account"

  Scenario: Generating the enrolment capability a second time changes nothing
    Given an enrolment connects a child, a centre, arrangements, tags, and payee accounts
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

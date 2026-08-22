Feature: A centre records its service offerings, operating hours, and registration details

  A centre currently records only its identity, contact, and capacity
  details. It cannot yet say what services it offers (before- and
  after-school care, occasional care, and so on), when it is open each day,
  its Australian Company Number, its service care type (centre-based day
  care, family day care, outside-school-hours care), or its geographic
  location — all needed to answer real operational and compliance
  questions about the centre.

  Note: the centre's structure-node hierarchy stays out of scope — Rooms
  stay a direct relationship on Centre, as already chosen for this sample
  (see `gaps.md`). This change only adds the fields and relationships
  listed above.

  Background:
    Given the child-care sample workspace

  Scenario: A centre records its service offerings
    Given a centre offering the services "Before school care" and "Vacation care"
    When the workspace is compiled
    Then compilation succeeds
    And the centre's service offerings include "Before school care" and "Vacation care"

  Scenario: A centre records its operating hours
    Given a centre open on "Monday" from "07:00" to "18:00"
    When the workspace is compiled
    Then compilation succeeds
    And the centre's operating hours include "Monday" from "07:00" to "18:00"

  Scenario: A centre records its service care type, registration number, and location
    Given a centre with the service care type "OSHC", the Australian Company Number "123 456 789", and a location
    When the workspace is compiled
    Then compilation succeeds
    And the centre's service care type is "OSHC"
    And the centre's Australian Company Number is "123 456 789"
    And the centre has a recorded location

  Scenario: A centre with no Australian Company Number still compiles unchanged
    Given a centre with the service care type "CBC" and no Australian Company Number
    When the workspace is compiled
    Then compilation succeeds
    And the centre has no Australian Company Number

  Scenario: Generating the sample workspace a second time reports no changes
    Given the service offering "Before school care" has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

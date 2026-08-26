Feature: A centre records its operations and organisational structure

  A centre records the services it offers, when it operates, its
  registration details, its location, and where it sits in the
  organisation. Rooms belong to the centre through that organisational
  structure instead of through a separate direct relationship.

  Background:
    Given the child-care sample workspace

  Scenario: A centre records its service offerings
    Given the service offerings "Before school care" and "Vacation care"
    And a centre offers both services
    When the workspace is compiled
    Then compilation succeeds
    And the centre's service offerings include "Before school care" and "Vacation care"

  Scenario: A centre records its operating hours
    Given a centre open on "Monday" from "07:00" to "18:00"
    When the workspace is compiled
    Then compilation succeeds
    And the centre's operating hours include "Monday" from "07:00" to "18:00"

  Scenario: A centre records its service care type, registration number, and coordinates
    Given a centre with the service care type "OSHC"
    And its Australian Company Number is "123 456 789"
    And its latitude is "-27.4575" and its longitude is "153.0340"
    When the workspace is compiled
    Then compilation succeeds
    And the centre's service care type is "OSHC"
    And the centre's Australian Company Number is "123 456 789"
    And the centre's latitude is "-27.4575" and its longitude is "153.0340"

  Scenario: A centre with no Australian Company Number still compiles unchanged
    Given a centre with the service care type "CBC" and no Australian Company Number
    When the workspace is compiled
    Then compilation succeeds
    And the centre has no Australian Company Number

  Scenario: A centre belongs to an organisational structure
    Given a region named "Brisbane North" that can contain centres
    And a district named "Inner North" whose parent is "Brisbane North"
    And the centre belongs to "Inner North"
    When the workspace is compiled
    Then compilation succeeds
    And "Brisbane North" is the parent of "Inner North"
    And "Inner North" contains the centre

  Scenario: Rooms are reached through the centre's structure
    Given the centre belongs to the structure node "Inner North"
    And the room "Sunflower Room" belongs to that centre
    When the workspace is compiled
    Then compilation succeeds
    And the centre's structure nodes include "Inner North"
    And the centre has no separate direct Rooms relationship

  Scenario: Generating the sample workspace a second time reports no changes
    Given the service offering "Before school care" has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

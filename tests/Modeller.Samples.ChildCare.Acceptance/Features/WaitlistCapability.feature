Feature: A centre records a child's requested pattern of care

  A waitlist entry records when a child wants care at one centre. It can
  identify a preferred room and records the required or flexible weekdays
  in a fortnightly cycle. An end reason explains why the request ended.

  A waitlist entry expresses a request for care. It does not create a
  booking. Family details belong to the Family and Related adult capability.

  Background:
    Given the child-care sample workspace

  Scenario: A waitlist entry identifies its child and centre
    Given the child "Alex Smith" requests care at the centre "River Street"
    When the waitlist entry is recorded
    Then the waitlist entry is for the child "Alex Smith"
    And the waitlist entry is owned by the centre "River Street"

  Scenario: A waitlist entry records its requested care period
    Given a waitlist entry was created on 1 September 2026
    And its preferred care period starts on 5 October 2026
    And its preferred care period ends on 18 December 2026
    When the waitlist entry is reviewed
    Then its creation date is 1 September 2026
    And its preferred start date is 5 October 2026
    And its preferred end date is 18 December 2026

  Scenario: A waitlist entry can remain open without an end date or end reason
    Given a child has an open waitlist entry
    When the waitlist entry is reviewed
    Then it has no preferred end date
    And it has no end reason

  Scenario: A waitlist entry records required and flexible days in its fortnightly cycle
    Given a waitlist entry is for cycle week 2
    And Monday is required
    And Wednesday is flexible
    When the requested pattern of care is reviewed
    Then the waitlist entry contains a required Monday
    And the waitlist entry contains a flexible Wednesday
    And both waitlist days belong to that waitlist entry

  Scenario: A waitlist entry can identify a preferred room
    Given a child has a waitlist entry at the centre "River Street"
    And the preferred room is "Koala Room"
    When the waitlist entry is reviewed
    Then its preferred room is "Koala Room"

  Scenario: An ended waitlist entry records why it ended
    Given a child has a waitlist entry at the centre "River Street"
    And the waitlist entry ended because "Care found elsewhere"
    When the waitlist entry is reviewed
    Then its end reason is "Care found elsewhere"

  Scenario: A waitlist entry remains a request for care
    Given a child has a waitlist entry for a required Monday
    When the requested pattern of care is reviewed
    Then the waitlist entry does not create a booking

  Scenario: Generating the waitlist capability a second time changes nothing
    Given a waitlist entry connects a child, a centre, waitlist days, a room, and an end reason
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

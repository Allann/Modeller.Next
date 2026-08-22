Feature: A room can carry a nickname alongside its number

  A room is currently identified by its ordinal number and its name. Centres
  also give rooms an informal nickname (drawn from a shared, reusable list —
  "Possums", "Kookaburras") used in day-to-day conversation and on signage.
  Today the sample has no way to record that nickname.

  Note: the room's status stays a plain status field for this sample, as a
  deliberate simplification (matching the same choice already made for
  Charge) rather than the legacy audit-log-style status history. That
  simplification is not part of this change.

  Background:
    Given the child-care sample workspace

  Scenario: A room records a nickname from the shared nickname list
    Given a room named "Sunflower Room" with the nickname "Possums"
    When the workspace is compiled
    Then compilation succeeds
    And the room's nickname is "Possums"

  Scenario: A room with no nickname still compiles unchanged
    Given a room named "Sunflower Room" with no nickname
    When the workspace is compiled
    Then compilation succeeds
    And the room has no nickname

  Scenario: Generating the sample workspace a second time reports no changes
    Given the nickname "Possums" has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

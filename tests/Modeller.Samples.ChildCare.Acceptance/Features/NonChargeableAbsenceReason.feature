Feature: A non-chargeable absence records its own reason

  An absence carries a general absence reason (why the child was away — sick,
  holiday, and so on) and, separately, a non-chargeable flag for absences the
  centre chooses not to bill (goodwill, extended leave). Today the sample
  model has no way to record *why* an absence was made non-chargeable: that
  reason is conflated with the general absence reason. The legacy model kept
  a distinct non-chargeable reason, and this sample should too, so a
  non-chargeable absence can carry a reason of its own.

  Background:
    Given the child-care sample workspace

  Scenario: A non-chargeable absence records a reason distinct from its absence reason
    Given an absence for a booked session, marked non-chargeable, with the non-chargeable reason "Goodwill gesture" and the absence reason "Sick"
    When the workspace is compiled
    Then compilation succeeds
    And the absence's non-chargeable reason is "Goodwill gesture"
    And the absence's absence reason is "Sick"

  Scenario: An absence with no non-chargeable reason still compiles unchanged
    Given an absence for a booked session that is not marked non-chargeable and carries no non-chargeable reason
    When the workspace is compiled
    Then compilation succeeds
    And the absence has no non-chargeable reason

  Scenario: Generating the sample workspace a second time reports no changes
    Given the non-chargeable reason "Goodwill gesture" has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

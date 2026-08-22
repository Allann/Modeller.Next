Feature: A child records community support and specialised support required

  Child cannot yet record which community support programs it draws on
  (from a shared, reusable list), or which kinds of specialised support the
  child requires (also from a shared, reusable list, kept as its own field
  distinct from any medical record — a deliberate split in the legacy model
  because these values apply to both additional needs and medical
  conditions).

  Note: medical records and consent stay out of scope for this change — see
  gaps.md's "untouched capability areas" list.

  Background:
    Given the child-care sample workspace

  Scenario: A child records the community support it draws on
    Given a child receiving the community support "Kindergarten fee subsidy"
    When the workspace is compiled
    Then compilation succeeds
    And the child's community support includes "Kindergarten fee subsidy"

  Scenario: A child records the specialised support they require
    Given a child requiring the specialised support "Speech therapy"
    When the workspace is compiled
    Then compilation succeeds
    And the child's specialised support required includes "Speech therapy"

  Scenario: A child with no community support or specialised support required still compiles unchanged
    Given a child with no community support and no specialised support required
    When the workspace is compiled
    Then compilation succeeds
    And the child has no community support and no specialised support required

  Scenario: Generating the sample workspace a second time reports no changes
    Given the community support "Kindergarten fee subsidy" has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

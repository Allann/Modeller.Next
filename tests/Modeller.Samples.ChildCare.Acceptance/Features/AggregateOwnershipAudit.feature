Feature: Ported child-care entities keep supported legacy aggregate ownership

  The child-care sample has enough RML ownership support to preserve aggregate
  owner facts from the legacy model. Each currently ported entity with a
  matching legacy owner fact should declare that owner when the owner entity is
  also present in the sample.

  Background:
    Given the child-care sample workspace

  Scenario: Ported entities with legacy owner evidence declare their owner
    Given the aggregate ownership audit has been applied to the sample workspace
    When the workspace is compiled
    Then compilation succeeds
    And each audited ported entity declares its supported legacy owner

  Scenario: Generating the ownership-audited model a second time reports no changes
    Given the aggregate ownership audit generation model has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

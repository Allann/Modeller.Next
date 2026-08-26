Feature: Read-only generation preview

  The playground can ask for a preview of the artifacts a template pack would
  generate from the current draft, without ever writing anything to a
  filesystem. A preview request names the workspace (its documents, identity,
  and configuration) and the template pack to apply. A successful preview
  returns the ordered list of proposed artifacts — each with its path, owner,
  template pack ID, and template ID — together with the rendered content for
  every artifact. A draft that fails to parse or validate, or that names a
  template pack the server does not recognize, still returns a preview
  response carrying diagnostics rather than an error, exactly as workspace
  analysis already does. A preview never writes generated output anywhere.

  Background:
    Given an empty workspace draft

  Scenario: A valid draft and a known template pack produce a generation preview
    Given a draft declaring one bounded context with an entity
    And the draft names the known template pack
    When a generation preview is requested for the draft
    Then the preview succeeds with no diagnostics
    And the preview lists the proposed artifacts in a stable order
    And every listed artifact carries its path, its owner, the template pack ID, and the template ID
    And every listed artifact carries its rendered content
    And nothing is written to a filesystem

  Scenario: Requesting a preview twice for the same draft returns identical artifacts
    Given a draft declaring one bounded context with an entity
    And the draft names the known template pack
    When a generation preview is requested for the draft
    And a generation preview is requested again for the same draft
    Then both previews list the same artifacts with identical rendered content

  Scenario: A draft that fails to parse still returns a preview response
    Given a draft with a syntax error
    And the draft names the known template pack
    When a generation preview is requested for the draft
    Then the preview reports diagnostics explaining the draft could not be parsed
    And the preview lists no artifacts

  Scenario: A draft that fails validation still returns a preview response
    Given a draft declaring one bounded context with an entity that has an invalid field
    And the draft names the known template pack
    When a generation preview is requested for the draft
    Then the preview reports diagnostics explaining the draft failed validation
    And the preview lists no artifacts

  Scenario: Naming an unknown template pack is reported as a diagnostic, not an error
    Given a draft declaring one bounded context with an entity
    And the draft names a template pack the server does not recognize
    When a generation preview is requested for the draft
    Then the preview reports a diagnostic explaining the template pack is unknown
    And the preview lists no artifacts

  Scenario: A draft whose generation contract does not match the template pack is reported as a diagnostic
    Given a draft declaring one bounded context with an entity
    And the draft declares a generation contract version the known template pack does not support
    When a generation preview is requested for the draft
    Then the preview reports a diagnostic explaining the generation contract is incompatible
    And the preview lists no artifacts

  Scenario: A draft that violates the request's own shape limits is rejected outright
    Given a draft with more documents than the preview request allows
    And the draft names the known template pack
    When a generation preview is requested for the draft
    Then the request is rejected as malformed
    And no diagnostics reference the draft's content

  Scenario: An ephemeral draft's preview does not persist any server-side session
    Given a draft declaring one bounded context with an entity
    And the draft names the known template pack
    When a generation preview is requested for the draft
    And a second, unrelated preview is requested for a different draft
    Then the second preview is unaffected by the first draft's content

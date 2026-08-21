Feature: Context map reveals cross-context dependencies

  A workspace may declare more than one bounded context. A bounded context may
  export a fact so that another bounded context can import it. The context map
  shows one node per bounded context and one dependency edge per declared
  import, so a workspace with real cross-context relationships never renders as
  a single disconnected node.

  Background:
    Given the workspace is empty

  Scenario: A workspace with two bounded contexts compiles
    Given a workspace declaring the bounded context "Child Care"
    And the workspace also declares the bounded context "Centre Operations"
    When the workspace is compiled
    Then compilation succeeds
    And the compiled model contains the bounded context "Child Care"
    And the compiled model contains the bounded context "Centre Operations"

  Scenario: A bounded context imports a fact exported by another bounded context
    Given a workspace declaring the bounded context "Child Care" that exports the fact "Active enrolment exists"
    And the workspace declares the bounded context "Centre Operations" that imports the fact "Active enrolment exists" from the bounded context "Child Care"
    When the workspace is compiled
    Then compilation succeeds
    And the compiled model records "Centre Operations" as depending on "Child Care" for the fact "Active enrolment exists"

  Scenario: Importing a fact that its owning bounded context has not exported is rejected
    Given a workspace declaring the bounded context "Child Care" with the fact "Active enrolment exists" that is not exported
    And the workspace declares the bounded context "Centre Operations" that imports the fact "Active enrolment exists" from the bounded context "Child Care"
    When the workspace is compiled
    Then compilation fails with a diagnostic explaining the fact is not exported

  Scenario: Importing from a bounded context that is not declared is rejected
    Given a workspace declaring the bounded context "Centre Operations" that imports the fact "Active enrolment exists" from the bounded context "Child Care"
    And no bounded context named "Child Care" is declared in the workspace
    When the workspace is compiled
    Then compilation fails with a diagnostic explaining the bounded context cannot be resolved

  Scenario: Two bounded contexts cannot share a name
    Given a workspace declaring two bounded contexts both named "Child Care"
    When the workspace is compiled
    Then compilation fails with a diagnostic explaining bounded context names must be unique

  Scenario: The context map shows a dependency edge for a declared import
    Given a compiled workspace where the bounded context "Centre Operations" imports the fact "Active enrolment exists" from the bounded context "Child Care"
    When the context map view is projected rooted at the bounded context "Child Care"
    Then the context map shows a node for the bounded context "Child Care"
    And the context map shows a node for the bounded context "Centre Operations"
    And the context map shows a dependency edge from "Centre Operations" to "Child Care" for the fact "Active enrolment exists"

  Scenario: The context map for a workspace with only one bounded context still succeeds with no dependency edges
    Given a compiled workspace declaring only the bounded context "Child Care"
    When the context map view is projected rooted at the bounded context "Child Care"
    Then the context map shows a node for the bounded context "Child Care"
    And the context map shows no dependency edges

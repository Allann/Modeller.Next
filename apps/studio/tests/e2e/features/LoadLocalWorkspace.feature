Feature: Load a local workspace into the Playground

  Today the Playground always starts from a small, hand-maintained copy of the
  child-care and ordering samples, baked into the app and kept in sync with
  the real samples by hand. A developer running the Playground on their own
  machine wants to point it at a real workspace directory instead — the
  child-care sample, the ordering sample, or any other local workspace — and
  see its actual modeller documents, not a stale copy.

  This capability only exists when the Playground is running locally, on a
  developer's own machine. The public, hosted Playground that anonymous
  visitors use has no local workspace to point at and must keep working
  exactly as it does today, from its bundled example.

  Background:
    Given the Playground is running locally

  Scenario: Loading a real sample workspace by directory path
    When a developer points the Playground at the "samples/child-care" workspace directory
    Then the Playground shows the modeller documents found in that workspace's model folder
    And the documents shown match the real files on disk, not the bundled example

  Scenario: Switching to a different local workspace without restarting
    Given the developer has already loaded the "samples/child-care" workspace directory
    When the developer points the Playground at the "samples/ordering" workspace directory instead
    Then the Playground replaces its documents with the ones found under "samples/ordering"

  Scenario: Loading a workspace directory outside the samples folder
    When the developer points the Playground at a workspace directory outside "samples/"
    Then the Playground shows the modeller documents found in that directory the same way as for a sample workspace

  Scenario: Editing a loaded document does not touch the file on disk until saved
    Given the developer has loaded the "samples/child-care" workspace directory
    When the developer edits a document in the Playground
    Then the file on disk is unchanged
    And the Playground's session reflects the edit
    When the developer explicitly saves the document
    Then the file on disk matches the edited content

  Scenario: Pointing at a directory that isn't a recognised workspace
    Given the developer has loaded the "samples/child-care" workspace directory
    When the developer points the Playground at a directory with no workspace configuration
    Then the Playground reports that the directory isn't a recognised workspace
    And the previously loaded workspace stays in place

  Scenario: The public Playground refuses to load a local workspace
    Given the Playground is running in its public, hosted mode
    When a visitor's session attempts to load a workspace from a local directory
    Then the Playground refuses the request
    And the visitor's session keeps working from its normal bundled example

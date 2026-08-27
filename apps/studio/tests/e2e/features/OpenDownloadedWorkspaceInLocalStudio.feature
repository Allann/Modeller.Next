Feature: Open a downloaded workspace in local Modeller Studio

  The wiki and hosted playground promise that a reader can try a workspace,
  download it, open it in local Modeller Studio, and continue work without a
  repository clone, a terminal, Node, or visible .NET setup. The downloaded
  package is a complete Modeller workspace package. Local Studio is the app
  that owns that package type and opens it directly.

  The first slice targets Windows. The reader path is a Windows installer,
  Windows file association for ".modeller-workspace", and double-click or open
  directly into Studio. Studio bundles or hides the required runtimes and local
  Modeller tools. The reader does not see a developer setup path as the primary
  path.

  Background:
    Given the wiki landing page links to the hosted playground
    And the hosted playground shows a sample workspace

  Scenario: A reader opens a downloaded workspace in local Studio on Windows
    When a reader chooses to take the sample workspace local
    Then the playground downloads one ".modeller-workspace" package
    And the package contains the source documents
    And the package contains ".modeller/config.json"
    And the package contains a durable identity registry where durable identities apply
    When the reader double-clicks or opens the package on Windows with local Modeller Studio installed
    Then local Studio opens directly to that workspace
    And local Studio shows the same documents as the hosted playground
    And local Studio shows diagnostics for the opened workspace without using the hosted API
    And the reader is not asked to choose a repository, runtime, command, package manager, or SDK
    And the required local runtimes and Modeller tools are bundled or hidden from the reader

  Scenario: Local edits are saved to the opened workspace
    Given a reader opened a downloaded workspace in local Studio
    When the reader changes a source document
    And the reader saves the document
    Then the change is written to the opened workspace on disk
    And reopening the same workspace shows the saved change
    And the package identities remain stable for unchanged concepts

  Scenario: Local generation runs from the opened workspace
    Given a reader opened a downloaded workspace in local Studio
    When the reader asks Studio to generate from the opened workspace
    Then local generation completes from the opened workspace
    And the generated artifacts match the opened workspace
    And generation does not use the hosted API
    And the reader is not asked to install or choose a developer toolchain

  Scenario: The main reader path avoids developer setup terms
    When a reader reads the wiki local-use path
    Then the primary path says to try the playground, download the workspace, install Studio for Windows, open the package, and see the workspace
    And the primary path does not use "clone", "npm", "dotnet", "build", "checkout", "SDK", or "package manager"
    And any developer fallback is separate from the primary reader path

  Scenario: Studio is not installed when the package is opened
    Given a reader has downloaded a ".modeller-workspace" package
    And local Modeller Studio is not installed
    When the reader opens the package or follows the open-locally path on Windows
    Then the reader is offered the Windows Studio installer
    And the package is kept available after Studio is installed
    And opening the package after installation opens the workspace directly in Studio
    And the reader does not have to clone the repository, use a terminal, install a package manager, install an SDK, or choose a runtime

  Scenario: Studio owns the workspace package type on Windows
    Given local Modeller Studio is installed on Windows
    When a reader sees a ".modeller-workspace" package in Windows
    Then Windows shows it as a Modeller Studio workspace package
    And opening the package starts Studio
    And Studio opens the package workspace directly

  Scenario: A package cannot be opened safely
    Given a reader has a downloaded workspace package
    When local Studio cannot open the package because it is corrupt, unsupported, or blocked by file permissions
    Then Studio reports the reason in plain language
    And Studio leaves any existing open workspace unchanged
    And Studio offers the simplest safe recovery action, such as downloading again, updating Studio, or choosing an allowed folder
    And Studio does not show a raw exception or developer command as the main message

  Scenario: The package flow is deterministic
    Given the hosted playground contains an unchanged workspace
    When the workspace is downloaded twice
    Then both packages contain the same logical workspace files
    And both packages use deterministic package metadata
    When each package is opened in local Studio
    Then Studio observes the same documents, configuration, identities, diagnostics, and generated artifacts for both packages

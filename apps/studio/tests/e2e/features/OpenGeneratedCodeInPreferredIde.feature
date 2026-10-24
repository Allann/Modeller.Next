Feature: Open generated code in a preferred IDE

  Local Modeller Studio owns workspace authoring and generation. The generated
  project remains the reader's code, and the reader continues work in the IDE
  that Windows uses by default. Studio does not build, test, or run generated
  code in this first phase.

  Background:
    Given a reader has a local Modeller workspace open in Studio
    And the workspace has a valid template pack

  Scenario: A reader opens the generated output folder
    When the reader generates code from the workspace
    And the reader chooses to open the generated output folder
    Then Windows opens the folder that contains the generated project
    And the folder contains the generated solution and project files

  Scenario: A reader opens the template pack's generated project in their default IDE
    Given code has been generated from the workspace
    When the reader chooses to open the generated project in their IDE
    Then Windows opens the template pack's generated project entry point with the reader's default IDE
    And the reader can continue work in that IDE
    And Studio does not ask the reader to choose or install an IDE

  Scenario: A reader has not generated code yet
    When the reader chooses to open generated code
    Then Studio explains that the reader must generate code first
    And Studio keeps the workspace open

  Scenario: A template pack has no generated project entry point
    Given the template pack does not declare a generated project entry point
    When the reader chooses to open generated code in their IDE
    Then Studio explains that this template pack has no generated project entry point to open
    And Studio offers to open the generated output folder
    And Studio keeps the workspace open

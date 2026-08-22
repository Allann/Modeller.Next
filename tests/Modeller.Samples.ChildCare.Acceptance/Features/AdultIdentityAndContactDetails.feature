Feature: An adult has real identity and contact details

  Adult is referenced by Absence, Arrangement, and elsewhere, but today it
  is an empty identity-only stub — it has no name, date of birth, CRN, or
  contact details of its own. This gives it the core identity and contact
  fields the legacy model already establishes, so an adult in this sample
  is a real person, not a placeholder.

  Note: the adult's richer relationships (title, gender, ethnic background,
  languages, addresses, employment status, education, CCSS confirmation)
  stay out of scope for this change — see gaps.md's "untouched capability
  areas" list.

  Background:
    Given the child-care sample workspace

  Scenario: An adult records their name, date of birth, and CRN
    Given an adult named "Jane Smith", born "1985-04-12", with the CRN "AB123456"
    When the workspace is compiled
    Then compilation succeeds
    And the adult's name is "Jane Smith"
    And the adult's date of birth is "1985-04-12"
    And the adult's CRN is "AB123456"

  Scenario: An adult records their contact details
    Given an adult with the home phone "07 3123 4567", the mobile phone "0412 345 678", and the email "jane.smith@example.com"
    When the workspace is compiled
    Then compilation succeeds
    And the adult's home phone is "07 3123 4567"
    And the adult's mobile phone is "0412 345 678"
    And the adult's email is "jane.smith@example.com"

  Scenario: An adult with no date of birth, CRN, or contact details still compiles unchanged
    Given an adult with only a name
    When the workspace is compiled
    Then compilation succeeds
    And the adult has no date of birth, no CRN, and no contact details

  Scenario: Generating the sample workspace a second time reports no changes
    Given the adult "Jane Smith" has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

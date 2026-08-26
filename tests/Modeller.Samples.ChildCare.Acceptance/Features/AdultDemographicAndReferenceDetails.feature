Feature: An adult records demographic and reference details

  An adult can record demographic, language, address, employment, education,
  and government-confirmed details. Catalogue-backed concepts remain reusable
  domain references rather than arbitrary text on the adult.

  This capability extends the adult's existing identity and contact details.
  It does not define organisation membership, staff roles, security,
  notifications, or ownership.

  Background:
    Given the child-care sample workspace

  Scenario: An adult selects reusable title, gender, and ethnic background details
    Given the adult "Jane Smith" has the title "Ms"
    And the adult has the gender "Woman"
    And the adult has the ethnic backgrounds "Māori" and "Irish"
    When the adult's demographic details are reviewed
    Then the title identifies the reusable title "Ms"
    And the gender identifies the reusable gender "Woman"
    And both ethnic backgrounds identify reusable ethnic background entries
    And none of these details is arbitrary text on the adult

  Scenario: An adult records more than one spoken language
    Given the adult "Jane Smith" speaks "English" and "Te Reo Māori"
    When the adult's languages are reviewed
    Then both languages identify reusable language entries
    And each language remains available for another adult

  Scenario: An adult records typed addresses in shared states
    Given the adult "Jane Smith" has a residential address at "12 River Street", "Brisbane", "4000", "Queensland"
    And the adult has a postal address at "PO Box 25", "Brisbane", "4001", "Queensland"
    When the adult's addresses are reviewed
    Then both addresses belong to that adult
    And each address has its selected address type
    And each address identifies the reusable state "Queensland"
    And the address types are selected from Residential, Commercial, and Postal

  Scenario: An adult records employment and education references
    Given the adult "Jane Smith" has the employment statuses "Employed" and "Self-employed"
    And the adult's highest education received is "Bachelor degree"
    When the adult's work and education details are reviewed
    Then both employment statuses identify reusable employment status entries
    And the highest education received identifies a reusable education entry

  Scenario: An adult can identify government-confirmed adult details
    Given the adult "Jane Smith" identifies government-confirmed adult details
    And those confirmed details have the service identifier "1234567890"
    And those confirmed details have the CRN "AB123456C"
    And those confirmed details have the date of birth "1985-04-12"
    When the adult's government-confirmed details are reviewed
    Then the confirmed details belong to that adult
    And the service identifier is "1234567890"
    And the CRN is "AB123456C"
    And the date of birth is "1985-04-12"

  Scenario: Demographic and reference details remain optional
    Given an adult with only their existing identity details
    When the workspace is compiled
    Then compilation succeeds
    And the adult has no title, gender, ethnic backgrounds, languages, addresses, employment statuses, highest education received, or government-confirmed adult details

  Scenario: Generating the adult details a second time changes nothing
    Given an adult has demographic, language, address, employment, education, and government-confirmed details
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged


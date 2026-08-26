Feature: A family connects children, related adults, accounts, and enrolments

  A family groups its children and the adults who have a family-specific
  relationship to them. The family owns one family account, while each care
  arrangement continues to name the account that pays for that arrangement.

  An adult is the person. A related adult records that person's place in one
  family. A family account records the family's financial responsibility.
  These meanings remain separate even when one adult fulfils all roles.

  Background:
    Given the child-care sample workspace

  Scenario: A family records its identity and origin
    Given the family "Smith, Jane" came to the centre through "Community referral"
    And its referral source is "Neighbourhood house"
    When the family is reviewed
    Then its family name is "Smith, Jane"
    And its pathway to the centre is "Community referral"
    And its referral source is "Neighbourhood house"

  Scenario: A family groups children without replacing their identities
    Given the family "Smith, Jane" includes the children "Alex Smith" and "Sam Smith"
    When the family is reviewed
    Then both children belong to that family
    And "Alex Smith" and "Sam Smith" remain distinct children

  Scenario: A related adult gives an adult a place in one family
    Given the adult "Jane Smith" is related to the family "Smith, Jane"
    And the relationship type is "Parent"
    And the display priority is 1
    And the related adult has the authorisations "Collect child" and "Approve medical treatment"
    When the family's related adults are reviewed
    Then "Jane Smith" is the first related adult displayed for that family
    And the relationship type is "Parent"
    And both authorisations belong to that related adult relationship
    And "Jane Smith" remains an adult independently of that family relationship

  Scenario: A family owns one family account with ranked adult holders
    Given the family "Smith, Jane" owns the family account "Smith family account"
    And the family account uses the account "FA-1001"
    And the adult "Jane Smith" is its first account holder
    And the adult "Morgan Smith" is its second account holder
    When the family account is reviewed
    Then "Jane Smith" and "Morgan Smith" are jointly responsible through distinct ranked account-holder records
    And neither account holder is made a related adult by financial responsibility alone

  Scenario: An enrolment connects a child and family to their arrangements
    Given the child "Alex Smith" belongs to the family "Smith, Jane"
    And that child has an enrolment at the centre "River Street"
    And the enrolment has the arrangement "Before school care"
    When the enrolment is reviewed
    Then the enrolment identifies the child "Alex Smith"
    And the enrolment identifies the family "Smith, Jane"
    And "Before school care" belongs to that enrolment

  Scenario: An arrangement keeps its own payee account
    Given the family "Smith, Jane" owns the family account "Smith family account"
    And the family account uses the account "FA-1001"
    And the enrolment has the arrangement "Before school care"
    And that arrangement is paid by the account "FA-1001"
    When the family's care and financial relationships are reviewed
    Then the arrangement payee is the account "FA-1001"
    And the arrangement does not become the family account

  Scenario: Generating the family capability a second time changes nothing
    Given a family connects its children, related adults, family account, enrolment, and arrangements
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

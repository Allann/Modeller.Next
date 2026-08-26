Feature: Report care and record government subsidy entitlements

  A centre connects a government-confirmed child to a care arrangement through
  a government enrolment occurrence. The centre reports the arrangement's
  delivered sessions for one week and records the subsidy entitlements returned
  for those sessions.

  An ACCS arrangement uses the same reporting path after its ACCS determination
  has made the arrangement eligible. Payments, family details, personnel, and
  notifications belong to separate capabilities.

  Background:
    Given the child-care sample workspace

  Scenario: A confirmed child can have a government enrolment occurrence
    Given the child "Alex Smith" has confirmed government details
    And "Alex Smith" has the arrangement "Before school care" at "River Street"
    When the government enrolment occurrence is recorded
    Then the occurrence identifies the government enrolment
    And the occurrence belongs to the arrangement "Before school care"
    And the occurrence records its government stage and visible stage

  Scenario: An unconfirmed child cannot have a reportable enrolment occurrence
    Given the child "Sam Jones" has no confirmed government details
    And "Sam Jones" has the arrangement "Vacation care" at "River Street"
    When government enrolment readiness is determined
    Then the arrangement is not ready for a government enrolment occurrence
    And the finding states that confirmed child details are required

  Scenario: A centre submits one weekly report for delivered sessions
    Given the arrangement "Before school care" has an active government enrolment occurrence
    And its week starting "2026-08-24" contains the delivered sessions "Monday care" and "Tuesday care"
    When the centre submits the weekly session report
    Then the report belongs to the arrangement "Before school care"
    And the report starts on "2026-08-24"
    And the report contains "Monday care" and "Tuesday care"
    And the report advances from Draft to Submitted

  Scenario: A weekly report needs an active government enrolment occurrence
    Given the arrangement "Vacation care" has no active government enrolment occurrence
    And its week starting "2026-08-24" contains the delivered session "Wednesday care"
    When session-report readiness is determined
    Then the weekly session report is not ready for submission
    And the finding states that an active government enrolment occurrence is required

  Scenario: Returned subsidy results explain each session entitlement
    Given the submitted weekly report for "Before school care" starts on "2026-08-24"
    When government subsidy results are recorded
    Then the result records the weekly fee, care hours, entitlement amount, subsidised hours, and absence count
    And each session entitlement identifies its delivered session
    And each session entitlement records the amount, subsidised hours, recipient, and entitlement type
    And a nil or partial session entitlement can record a reason

  Scenario: An eligible ACCS arrangement uses the same weekly reporting path
    Given the arrangement "At-risk care" is an ACCS arrangement
    And its ACCS determination is eligible
    And it has an active government enrolment occurrence
    When the centre submits its weekly session report
    Then the report follows the government subsidy reporting lifecycle
    And the ACCS determination is not duplicated by the reporting capability

  Scenario: Generating the government subsidy capability a second time changes nothing
    Given confirmed child details, an enrolment occurrence, a weekly session report, and subsidy entitlements
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

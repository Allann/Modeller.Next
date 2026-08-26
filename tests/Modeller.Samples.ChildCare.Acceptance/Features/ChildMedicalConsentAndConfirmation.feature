Feature: A child records medical, consent, additional-needs, and CCSS confirmation details

  Background:
    Given the child-care sample workspace

  Scenario: Child wellbeing and support records retain their legacy-derived domain shape
    Given the legacy-derived Child wellbeing and support model
    When the workspace is compiled
    Then compilation succeeds
    And the child has consent, medical record, additional needs, and CCSS confirmation relationships
    And the medical record retains its alerts, review date, dietary requirements, conditions, and immunisation statuses
    And an additional need retains its dates, diagnosis, comments, and specialised support
    And the CCSS confirmation retains its service identifier, CRN, and date of birth

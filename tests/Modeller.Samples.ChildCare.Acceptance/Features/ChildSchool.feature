Feature: A school-aged child records their school

  Child already records whether it is school-aged, but has no way to say
  which school the child attends, its classroom, or the year the child
  started school. This adds that relationship using a shared, reusable
  school list.

  Background:
    Given the child-care sample workspace

  Scenario: A school-aged child records their school, classroom, and start year
    Given a school-aged child at the school "Fortitude Valley State School" in the classroom "3B", starting in "2023"
    When the workspace is compiled
    Then compilation succeeds
    And the child's school is "Fortitude Valley State School"
    And the child's classroom is "3B"
    And the child's school start year is "2023"

  Scenario: A child who is not yet school-aged still compiles with no school recorded
    Given a child who is not school-aged, with no school recorded
    When the workspace is compiled
    Then compilation succeeds
    And the child has no school

  Scenario: Generating the sample workspace a second time reports no changes
    Given the school "Fortitude Valley State School" has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

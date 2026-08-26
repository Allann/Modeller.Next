Feature: Workforce access is limited by organisation, role, right, and structure node

  The child-care sample needs a bounded workforce and access-control model.
  A user can belong to one or more organisations. An organisation employs staff,
  defines roles, and assigns those roles to users for specified structure nodes.
  A role grants named rights through rights groups. Access is allowed only when
  one current assignment joins the requesting user, organisation, role, required
  right, and requested structure node.

  This slice uses exact structure-node assignments. It does not grant access to
  parent or child nodes by inheritance. Authentication, role administration,
  notifications, and audit history are outside this feature.

  Background:
    Given the child-care sample workspace

  Scenario: A user can be a member of more than one organisation
    Given the user "Alex Chen" is a member of "Harbour Child Care" and "River Child Care"
    When the workspace is compiled
    Then compilation succeeds
    And the user identifies both organisation memberships

  Scenario: An organisation records an employee for a user
    Given "Harbour Child Care" employs the user "Alex Chen"
    And the employee has external employee identifier "EMP-1042"
    And the employee is named "Alex Chen"
    And the employee has occupation code "EDUCATOR"
    And the employee has authentication subject identifier "subject-1042"
    When the workspace is compiled
    Then compilation succeeds
    And the employee belongs to "Harbour Child Care"
    And the employee identifies the user "Alex Chen"

  Scenario: A current role assignment grants its right at its structure node
    Given "Harbour Child Care" has the right "attendance_read"
    And the rights group "Attendance readers" contains the right "attendance_read"
    And the role "Educator" contains the rights group "Attendance readers"
    And the user "Alex Chen" is a member of "Harbour Child Care"
    And the user has the role "Educator" at structure node "Brisbane Centre" from 1 August 2026
    When access to "attendance_read" at "Brisbane Centre" is decided on 26 August 2026
    Then access is allowed

  Scenario: A role without the required right does not grant access
    Given the role "Educator" grants the right "attendance_read"
    And the user "Alex Chen" has the role "Educator" at structure node "Brisbane Centre"
    When access to "attendance_change" at "Brisbane Centre" is decided
    Then access is denied

  Scenario: A role assignment does not grant access at another structure node
    Given the role "Educator" grants the right "attendance_read"
    And the user "Alex Chen" has the role "Educator" at structure node "Brisbane Centre"
    When access to "attendance_read" at "Gold Coast Centre" is decided
    Then access is denied

  Scenario: A future role assignment does not grant access
    Given the role "Educator" grants the right "attendance_read"
    And the user "Alex Chen" has the role "Educator" at structure node "Brisbane Centre" from 1 September 2026
    When access to "attendance_read" at "Brisbane Centre" is decided on 26 August 2026
    Then access is denied

  Scenario: An ended role assignment does not grant access
    Given the role "Educator" grants the right "attendance_read"
    And the user "Alex Chen" had the role "Educator" at structure node "Brisbane Centre" until 25 August 2026
    When access to "attendance_read" at "Brisbane Centre" is decided on 26 August 2026
    Then access is denied

  Scenario: A role assignment is current on its start date
    Given the role "Educator" grants the right "attendance_read"
    And the user "Alex Chen" has the role "Educator" at structure node "Brisbane Centre" from 26 August 2026
    When access to "attendance_read" at "Brisbane Centre" is decided on 26 August 2026
    Then access is allowed

  Scenario: A role assignment is current on its end date
    Given the role "Educator" grants the right "attendance_read"
    And the user "Alex Chen" had the role "Educator" at structure node "Brisbane Centre" until 26 August 2026
    When access to "attendance_read" at "Brisbane Centre" is decided on 26 August 2026
    Then access is allowed

  Scenario: Membership in another organisation does not grant access
    Given the role "Educator" in "Harbour Child Care" grants the right "attendance_read"
    And the user "Alex Chen" is a member only of "River Child Care"
    And the user has the role "Educator" in "Harbour Child Care" at structure node "Brisbane Centre"
    When access to "attendance_read" at "Brisbane Centre" for "Harbour Child Care" is decided
    Then access is denied

  Scenario: A role from another organisation does not grant access
    Given the user "Alex Chen" belongs to "Harbour Child Care"
    And the role "Educator" belongs to "River Child Care"
    And structure node "Brisbane Centre" belongs to "Harbour Child Care"
    When access to "attendance_read" at "Brisbane Centre" for "Harbour Child Care" is decided
    Then access is denied

  Scenario: A structure node from another organisation does not grant access
    Given the user "Alex Chen" belongs to "Harbour Child Care"
    And the role "Educator" belongs to "Harbour Child Care"
    And structure node "Brisbane Centre" belongs to "River Child Care"
    When access to "attendance_read" at "Brisbane Centre" for "Harbour Child Care" is decided
    Then access is denied

  Scenario: A security assignment cannot cross organisation boundaries
    Given the user "Alex Chen" belongs to "Harbour Child Care"
    And the role "Educator" belongs to "Harbour Child Care"
    And structure node "Gold Coast Centre" belongs to "River Child Care"
    When that user, role, and structure node are combined in one security assignment
    Then the security assignment is invalid

  Scenario: A security assignment requires organisation membership
    Given the user "Alex Chen" is a member only of "River Child Care"
    And the role "Educator" belongs to "Harbour Child Care"
    And structure node "Brisbane Centre" belongs to "Harbour Child Care"
    When that user, role, and structure node are combined in one security assignment
    Then the security assignment is invalid

  Scenario: Generating the workforce model a second time reports no changes
    Given the bounded workforce and access-control model has been added to the sample workspace
    When the workspace is generated
    And the workspace is generated again
    Then the second generation reports every output as unchanged

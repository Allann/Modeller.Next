Feature: Entity declares its aggregate-root owner

  An entity may declare which other entity owns it — the DDD aggregate-root
  fact that says this entity only ever changes as part of its owner's
  consistency boundary. RML 1.0's entity grammar has never carried this fact:
  an entity like Absence has fields and relationships, but nothing states
  that Centre owns it. Declaring an owner is optional — an entity with no
  declared owner is itself an aggregate root (Centre has no owner of its
  own). An owner must name a real entity declared in the same workspace, the
  same way a relationship's target does, and ownership can never form a
  cycle: an aggregate root cannot end up owning itself, directly or through
  a chain of other entities.

  Scenario: An entity declares which entity owns it as its aggregate root
    Given a workspace declaring the context "Child Care", the entity "Centre", and the entity "Absence" owned by "Centre"
    When the workspace is compiled
    Then compilation succeeds
    And the compiled model records "Centre" as the aggregate-root owner of "Absence"

  Scenario: An entity with no declared owner is itself an aggregate root
    Given a workspace declaring the context "Child Care" and the entity "Centre" with no declared owner
    When the workspace is compiled
    Then compilation succeeds
    And the compiled model records that "Centre" has no aggregate-root owner

  Scenario: An entity can be owned by an entity that is itself owned by another
    Given a workspace declaring the context "Child Care", the entity "Centre", the entity "Room" owned by "Centre", and the entity "Absence" owned by "Room"
    When the workspace is compiled
    Then compilation succeeds
    And the compiled model records "Centre" as the aggregate-root owner of "Room"
    And the compiled model records "Room" as the aggregate-root owner of "Absence"

  Scenario: Declaring an owner that is not declared anywhere in the workspace is rejected
    Given a workspace declaring the context "Child Care" and the entity "Absence" owned by "Centre", where no entity named "Centre" is declared
    When the workspace is compiled
    Then compilation fails with a diagnostic explaining the owner entity cannot be resolved

  Scenario: An entity cannot declare itself as its own owner
    Given a workspace declaring the context "Child Care" and the entity "Absence" owned by "Absence"
    When the workspace is compiled
    Then compilation fails with a diagnostic explaining an entity cannot own itself

  Scenario: Two entities cannot own each other
    Given a workspace declaring the context "Child Care", the entity "Centre" owned by "Absence", and the entity "Absence" owned by "Centre"
    When the workspace is compiled
    Then compilation fails with a diagnostic explaining aggregate ownership cannot be circular

  Scenario: A longer ownership chain cannot loop back on itself
    Given a workspace declaring the context "Child Care", the entity "Centre" owned by "Room", the entity "Room" owned by "Absence", and the entity "Absence" owned by "Centre"
    When the workspace is compiled
    Then compilation fails with a diagnostic explaining aggregate ownership cannot be circular

  Scenario: A chain declared leaf-first still resolves to the correct aggregate roots
    Given a workspace declaring the context "Child Care", the entity "Absence" owned by "Room", the entity "Room" owned by "Centre", and the entity "Centre" with no declared owner
    When the workspace is compiled
    Then compilation succeeds
    And the compiled model records "Centre" as the aggregate-root owner of "Room"
    And the compiled model records "Room" as the aggregate-root owner of "Absence"
    And the compiled model records that "Centre" has no aggregate-root owner

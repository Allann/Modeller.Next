Feature: Behaviours declare published events and effects

  A behaviour explains what a system can do, but RML 1.0 has never let it say
  what happens as a result: which domain event it publishes, or what other
  effect it causes. Without that, two behaviours connected by one publishing
  an event the other reacts to have no way to show up as a real connection
  in the causality and event-flow view — and the behaviour map's event and
  effect nodes are always empty for RML-authored content.

  Scenario: A behaviour that publishes an event appears in the causality and event-flow view
    Given a workspace declaring the context "Child Care", the entity "Booking", and the behaviour "Record absence" for "Booking", which publishes the event "Absence recorded"
    When the workspace is compiled
    Then compilation succeeds
    When the causality and event-flow view is projected rooted at the context "Child Care"
    Then the view shows a node for the behaviour "Record absence"
    And the view shows a node for the event "Absence recorded"
    And the view shows a "publishes" relationship from "Record absence" to "Absence recorded"

  Scenario: Two behaviours connected by a published event both appear as a real event-flow chain
    Given a workspace declaring the context "Child Care", the entity "Booking", the behaviour "Record absence" for "Booking" which publishes the event "Absence recorded", and the behaviour "Run billing for booking" for "Booking" which publishes the event "Billing run completed"
    When the workspace is compiled
    Then compilation succeeds
    When the causality and event-flow view is projected rooted at the context "Child Care"
    Then the view shows a node for the behaviour "Record absence"
    And the view shows a node for the behaviour "Run billing for booking"
    And the view shows a "publishes" relationship from "Record absence" to "Absence recorded"
    And the view shows a "publishes" relationship from "Run billing for booking" to "Billing run completed"

  Scenario: A behaviour that causes an effect appears in the behaviour map
    Given a workspace declaring the context "Child Care", the entity "Booking", and the behaviour "Record absence" for "Booking", which causes the effect "Notify billing system"
    When the workspace is compiled
    Then compilation succeeds
    When the behaviour map view is projected rooted at the entity "Booking"
    Then the view shows a node for the behaviour "Record absence"
    And the view shows a node for the effect "Notify billing system"

  Scenario: A behaviour that declares both a published event and an effect shows both in the behaviour map
    Given a workspace declaring the context "Child Care", the entity "Booking", and the behaviour "Record absence" for "Booking", which publishes the event "Absence recorded" and causes the effect "Notify billing system"
    When the workspace is compiled
    Then compilation succeeds
    When the behaviour map view is projected rooted at the entity "Booking"
    Then the view shows a node for the event "Absence recorded"
    And the view shows a node for the effect "Notify billing system"

  Scenario: A behaviour that declares neither a published event nor an effect still compiles and shows no event-flow content
    Given a workspace declaring the context "Child Care", the entity "Booking", and the behaviour "Record absence" for "Booking", with no published event and no effect
    When the workspace is compiled
    Then compilation succeeds
    When the causality and event-flow view is projected rooted at the context "Child Care"
    Then the view shows no nodes and no relationships

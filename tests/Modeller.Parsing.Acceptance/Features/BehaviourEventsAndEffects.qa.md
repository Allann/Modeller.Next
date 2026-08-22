# QA procedure: Behaviours declare published events and effects

Proves that a behaviour can declare the domain event it publishes and the
effect it causes, and that both show up in the relevant diagram views —
instead of always rendering as an empty graph, as every RML-authored
behaviour does today.

Run this in Modeller Studio's playground (or an equivalent workspace editor
exposing the same RML source panes and diagram view).

## Setup

1. Open a new, empty workspace.
2. Add an RML source document declaring:
   - a context named "Child Care"
   - an entity named "Booking"

## Part 1 — a behaviour can declare a published event

1. In the workspace, declare a behaviour named "Record absence" for the
   "Booking" entity, and declare that it publishes an event named "Absence
   recorded".
2. Ask the workspace to compile (analyze).
3. Confirm compilation succeeds with no diagnostics.
4. Select the "Causality and event flow" diagram view, rooted at "Child
   Care".
5. Confirm the diagram shows a node for the behaviour "Record absence" and a
   node for the event "Absence recorded".
6. Confirm the diagram shows a line connecting "Record absence" to "Absence
   recorded", labelled or otherwise identified as "publishes".

## Part 2 — two behaviours connected by a published event form a real chain

1. Add a second behaviour, "Run billing for booking", for the "Booking"
   entity, and declare that it publishes an event named "Billing run
   completed".
2. Compile the workspace.
3. Confirm compilation succeeds with no diagnostics.
4. Select the "Causality and event flow" diagram view, rooted at "Child
   Care".
5. Confirm the diagram now shows both behaviours ("Record absence" and "Run
   billing for booking") and both events ("Absence recorded" and "Billing
   run completed"), each behaviour connected to the event it publishes.

## Part 3 — a behaviour can declare an effect

1. Starting from a fresh workspace with "Child Care" and "Booking" declared,
   add the behaviour "Record absence" for "Booking", and declare that it
   causes an effect named "Notify billing system".
2. Compile the workspace.
3. Confirm compilation succeeds with no diagnostics.
4. Select the "Behaviour map" diagram view, rooted at the "Booking" entity.
5. Confirm the diagram shows a node for the behaviour "Record absence" and a
   node for the effect "Notify billing system", connected.

## Part 4 — a behaviour can declare both a published event and an effect

1. Extend the "Record absence" behaviour from Part 3 to also publish the
   event "Absence recorded" (in addition to the "Notify billing system"
   effect).
2. Compile the workspace.
3. Confirm compilation succeeds with no diagnostics.
4. Select the "Behaviour map" diagram view, rooted at "Booking".
5. Confirm the diagram shows both the event node "Absence recorded" and the
   effect node "Notify billing system" for "Record absence".

## Part 5 — a behaviour with neither still works exactly as before

1. Starting from a fresh workspace with "Child Care" and "Booking" declared,
   add the behaviour "Record absence" for "Booking", declaring no published
   event and no effect.
2. Compile the workspace.
3. Confirm compilation succeeds with no diagnostics.
4. Select the "Causality and event flow" diagram view, rooted at "Child
   Care".
5. Confirm the diagram succeeds and shows no nodes and no connections, and
   that this is presented as an expected, correct result rather than an
   error.

## Pass criteria

All five parts behave as described. In particular: Part 2's two-behaviour
chain must show as two connected behaviour/event pairs in one diagram — this
is the exact scenario (a multi-step causal chain across behaviours) that
motivated this change.

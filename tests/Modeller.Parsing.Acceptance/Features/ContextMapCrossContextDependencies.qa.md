# QA procedure: Context map reveals cross-context dependencies

Proves that a workspace can declare more than one bounded context, that one
bounded context can import a fact another bounded context exports, and that
the context map diagram then shows a real dependency edge instead of a single
disconnected node.

Run this in Modeller Studio's playground (or an equivalent workspace editor
exposing the same RML source panes and diagram view).

## Setup

1. Open a new, empty workspace.

## Part 1 — a workspace may declare more than one bounded context

1. Add an RML source document declaring a bounded context named "Child Care".
2. Add a second RML source document declaring a bounded context named
   "Centre Operations".
3. Ask the workspace to compile (analyze) the two documents together.
4. Confirm compilation succeeds with no diagnostics.
5. Confirm the compiled workspace lists both "Child Care" and
   "Centre Operations" as bounded contexts.

## Part 2 — one bounded context can import a fact exported by another

1. In the "Child Care" document, declare a fact named "Active enrolment
   exists" and mark it as exported.
2. In the "Centre Operations" document, declare that "Centre Operations"
   imports the fact "Active enrolment exists" from the bounded context
   "Child Care".
3. Compile the workspace.
4. Confirm compilation succeeds with no diagnostics.
5. Confirm the compiled workspace records that "Centre Operations" depends on
   "Child Care" for "Active enrolment exists".

## Part 3 — importing a fact that was not exported is rejected

1. Starting from Part 2's workspace, remove the export mark from "Active
   enrolment exists" in the "Child Care" document (leave the fact declared,
   but not exported).
2. Compile the workspace.
3. Confirm compilation fails.
4. Confirm the reported diagnostic explains that "Active enrolment exists" is
   not exported by "Child Care".

## Part 4 — importing from an undeclared bounded context is rejected

1. Starting from a fresh empty workspace, add only the "Centre Operations"
   document from Part 2 (declaring the import from "Child Care"), without
   adding any "Child Care" document.
2. Compile the workspace.
3. Confirm compilation fails.
4. Confirm the reported diagnostic explains that the bounded context "Child
   Care" cannot be resolved.

## Part 5 — two bounded contexts cannot share a name

1. Starting from a fresh empty workspace, add two documents that each declare
   a bounded context named "Child Care".
2. Compile the workspace.
3. Confirm compilation fails.
4. Confirm the reported diagnostic explains that bounded context names must
   be unique.

## Part 6 — the context map shows a real dependency edge

1. Return to Part 2's successfully compiled workspace (two bounded contexts,
   one import).
2. Select the "Context map" diagram view, rooted at "Child Care".
3. Confirm the diagram shows two nodes: one for "Child Care" and one for
   "Centre Operations".
4. Confirm the diagram shows one edge running from "Centre Operations" to
   "Child Care".
5. Confirm that edge is labelled with (or otherwise identifies) the imported
   fact "Active enrolment exists".

## Part 7 — a single-context workspace still renders successfully

1. Return to a workspace declaring only "Child Care" (no second context, no
   imports) — for example, Part 1's workspace with the "Centre Operations"
   document removed.
2. Select the "Context map" diagram view, rooted at "Child Care".
3. Confirm the diagram succeeds and shows one node for "Child Care".
4. Confirm the diagram shows no dependency edges, and that this is presented
   as an expected, correct result rather than an error.

## Pass criteria

All seven parts behave as described. In particular: Part 3, Part 4, and
Part 5 must produce an explicit compilation failure with a diagnostic that a
person can read and act on — not a silent empty or partial result.

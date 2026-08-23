# QA procedure: Entity declares its aggregate-root owner

Proves that an entity can declare which other entity owns it (the DDD
aggregate-root fact) — instead of that fact silently being dropped, as it is
for every entity ported into the child-care sample today (`Absence` should
be owned by `Centre`, but the model carries no such fact).

Run this in Modeller Studio's playground (or an equivalent workspace editor
exposing the same RML source panes).

## Setup

1. Open a new, empty workspace.
2. Add an RML source document declaring a context named "Child Care".

## Part 1 — an entity can declare its owner

1. Declare an entity named "Centre" (no owner).
2. Declare an entity named "Absence" that is owned by "Centre".
3. Ask the workspace to compile (analyze).
4. Confirm compilation succeeds with no diagnostics.
5. Confirm the compiled model records "Centre" as the aggregate-root owner of
   "Absence" — for example, by inspecting the entity's details panel or an
   equivalent model inspector.

## Part 2 — declaring an owner is optional

1. Starting from a fresh workspace with "Child Care" declared, declare an
   entity named "Centre" with no owner clause at all.
2. Compile the workspace.
3. Confirm compilation succeeds with no diagnostics.
4. Confirm the compiled model shows "Centre" as having no aggregate-root
   owner — this is the expected, correct result for an aggregate root, not
   an error or a warning.

## Part 3 — ownership can chain through more than one level

1. Starting from a fresh workspace with "Child Care" declared, declare:
   - an entity "Centre" with no owner
   - an entity "Room" owned by "Centre"
   - an entity "Absence" owned by "Room"
2. Compile the workspace.
3. Confirm compilation succeeds with no diagnostics.
4. Confirm the compiled model records "Centre" as the owner of "Room", and
   "Room" as the owner of "Absence".

## Part 4 — an owner that does not exist is rejected

1. Starting from a fresh workspace with "Child Care" declared, declare only
   an entity "Absence" that is owned by "Centre" — without declaring any
   entity named "Centre".
2. Compile the workspace.
3. Confirm compilation fails.
4. Confirm the reported diagnostic explains that the declared owner "Centre"
   cannot be resolved to a real entity.

## Part 5 — an entity cannot own itself

1. Starting from a fresh workspace with "Child Care" declared, declare an
   entity "Absence" that declares itself as its own owner.
2. Compile the workspace.
3. Confirm compilation fails.
4. Confirm the reported diagnostic explains that an entity cannot own
   itself.

## Part 6 — two entities cannot own each other

1. Starting from a fresh workspace with "Child Care" declared, declare an
   entity "Centre" owned by "Absence", and an entity "Absence" owned by
   "Centre".
2. Compile the workspace.
3. Confirm compilation fails.
4. Confirm the reported diagnostic explains that aggregate ownership cannot
   be circular.

## Part 7 — a longer ownership chain cannot loop back on itself

1. Starting from a fresh workspace with "Child Care" declared, declare:
   - an entity "Centre" owned by "Room"
   - an entity "Room" owned by "Absence"
   - an entity "Absence" owned by "Centre"
2. Compile the workspace.
3. Confirm compilation fails.
4. Confirm the reported diagnostic explains that aggregate ownership cannot
   be circular — the same diagnostic as Part 6, even though no single pair
   of entities directly owns each other.

## Pass criteria

All seven parts behave as described. In particular: Parts 4 through 7 must
produce an explicit compilation failure with a diagnostic a person can read
and act on — not a silent empty or partial result, and not a stack overflow
or hang on the circular cases.

## Known follow-up work (not covered by this procedure)

Once this capability exists, two further changes are expected as separate
work, not part of this story:

- `samples/child-care/model/entities/absence.modeller` should be updated to
  add `owner "Centre"`, so the sample workspace itself carries the fact this
  procedure proves is possible.
- `samples/child-care/gaps.md`'s "Entity ownership (aggregate root) is not
  ported" section (which references this story's originating issue, #123)
  should be updated or removed once the fact has actually been ported for
  Absence and any other affected entities.

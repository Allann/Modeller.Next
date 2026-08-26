# Deferred from the legacy model

This slice ports a broad cross-section of `M:\Modeller\samples\child-care-old`
(domain evidence only, not a compatibility target) rather than every field of
every entity. Every known omission is either tracked by a bounded follow-up
issue, retained as an intentional simplification, or explicitly outside this
sample-port backlog.

## Tracked domain increments

- **Room**: the bounded #129 capability is ported. A Room has an optional
  reusable nickname and a required audit-style Room status record with status
  type, reason, date, and optional notes. A Room identifies its Centre.
- **Centre**: the bounded #129 capability is ported. A Centre records
  catalogue-backed service offerings, weekly operating hours, optional ACN,
  service care type, latitude, longitude, and organisational Structure nodes.
  Structure node types state whether they can contain centres, and nodes can
  refer to a parent. Centre no longer has a separate direct Rooms relationship.
  The model does not invent a Structure node-to-Room relationship.
- **Centre address**: the bounded #129 capability is ported. State is a
  required relationship to shared State code and name reference data.
- **Adult**: the bounded #130 capability is ported. Adult retains its core
  identity and contact fields and can identify reusable Title, Gender, ethnic
  backgrounds, Languages, employment statuses, and highest education received.
  Adult addresses reuse Address type and State. Optional CCSS-confirmed adult
  details record service identifier, CRN, and date of birth.
- **Workforce and access control**: the bounded #131 capability is ported.
  Users can belong to multiple Organisations. Organisation-owned Employee
  records connect staff details to Users. Organisation-owned Roles grant Rights
  through Rights groups. A dated Security assignment connects one User, Role,
  and exact Structure node. Access fails closed unless the user is a member,
  the assignment is current and organisation-consistent, its exact node matches,
  and the role grants the required right.
- **Enrolment**: the bounded capability is ported. An Enrolment identifies one
  Child and one Family, is owned by Centre, and groups Arrangements and
  Enrolment tags.
- **Waitlist**: the bounded capability is ported. A Centre-owned Waitlist
  identifies one Child, records its cycle and requested care dates, and owns
  required or flexible Waitlist days. Preferred Room, preferred end date, and
  end reason are optional. It has no direct Booking or Session relationship.
  Family details are available through the Child's Family and Enrolment graph.
- **Government subsidy reporting**: the bounded workflow from confirmed child
  details through a government enrolment occurrence, a weekly report of
  delivered Bookings, and returned weekly and per-session entitlements is
  ported. It reuses the existing ACCS determination workflow. Payments,
  reconciliation, family details, provider personnel, staff authorization, and
  notifications remain outside #127 and belong to their separate capabilities.
- **Family and Related adult**: the bounded capability is ported. Family groups
  Children and family-specific Related adults, owns one Family account, and
  records optional origin references. Related adult relationship types and
  authorisations are described reference entities. Ranked Family account
  holders connect financial responsibility to Adults. Debt collection,
  payment plans, notes, and tags remain outside this bounded increment.
- **Notifications**: the bounded #132 capability is ported for user-audience
  notifications. User notification is owned by Organisation, identifies one
  User, records subject, description, optional URL, type, and status, and moves
  through New, Viewed, and Completed. Centre/provider audience reference values
  are retained, but centre/provider workflows, delivery channels, retry queues,
  templates, external notification providers, and read receipts remain outside
  this bounded increment.

Authentication credentials, hierarchy-based access inheritance, and access
administration remain outside the bounded workforce capability.

## Intentional simplifications

- **Charge**: this sample intentionally synthesizes the legacy
  `AttendanceCharge` / `AttendanceChargeVersion` /
  `AttendanceChargeGroup` trio into one entity for the demonstrated billing
  behaviour. The legacy persistence history is not required for this slice.
- **Legacy breadth**: declarations not named above stay outside the sample.
  The legacy model is domain evidence, not a compatibility target or an
  exhaustive port checklist.

## Projection kinds this slice cannot demonstrate yet

`ContextMap` and `CausalityAndEventFlow` are implemented (issue #64) but stay
minimal here. These projection limits are outside the issue #122 domain-port
backlog:

- RML 1.0 now supports multiple `context` declarations per workspace and
  cross-context `import`s between them (issue #120), so `ContextMap` can show
  a real dependency edge. This sample stays single-context by choice — see
  the Studio Playground's child-care example for a version with a second
  "Centre Operations" context — so `ContextMap` intentionally renders a
  single node with no edges in this sample.
- RML 1.0's `behaviour` grammar has no syntax for declaring published events
  or effects, so `CausalityAndEventFlow` — which walks `BehaviourDefinition.PublishedEvents`
  — always renders an empty graph until events are exposed to authors.

`Lifecycle`, `RuleDecision`, `BehaviourMap`, and `Structural` all render real,
non-trivial content for this workspace.

## Entity ownership (aggregate root)

RML 1.0's `entity` grammar gained an `owner "<EntityName>"` clause (issue
#123), so the legacy `.key` file's `owner(...)` fact — e.g.
`M:\Modeller\samples\child-care-old\entities\Absence\Absence.key` states
`owner(Centre)` — has an equivalent again. The #133 audit matched every
currently ported entity to legacy `.key` owner facts, added each supported
owner declaration where the owner entity is also present in the sample, and
left only entities that are not ported outside this sample.

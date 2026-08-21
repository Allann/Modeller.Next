# Deferred from the legacy model

This slice ports a broad cross-section of `M:\Modeller\samples\child-care-old`
(domain evidence only, not a compatibility target) rather than every field of
every entity. What was simplified or left out, by area:

- **Child**: only core identity fields are ported. Medical records, consent,
  community support, additional-needs, school, and CCSS-confirmation
  relationships are out of scope.
- **Arrangement**: `Payee` (Account), `EndReason` (ArrangementEndReason), and
  `CRNHolder` (Adult) are not yet ported.
- **Absence**: the legacy `NonChargeableReason` relationship is conflated
  with `AbsenceReason` for this slice rather than kept as a separate entity.
- **Room**: `RoomNickname` is not ported. `Status` is simplified from the
  legacy audit-log-style `RoomStatus` entity to a plain `Room status type`
  enumeration field.
- **Centre**: `ServiceOfferings`, `OperatingHours`, `StructureNodes`, `ACN`,
  `ServiceCareType`, and the `Longitude`/`Latitude` fields are not ported.
  `Rooms` is modelled as a direct relationship, simplifying the legacy
  `StructureNode`-mediated link.
- **Centre address**: the legacy `State` entity relationship is simplified
  to a plain string field.
- **Charge**: synthesizes the legacy `AttendanceCharge` /
  `AttendanceChargeVersion` / `AttendanceChargeGroup` trio into one entity
  for this slice's billing behaviour.
- **Adult, User**: identity-only declarations (`model/entities/reference-stubs.modeller`)
  — referenced widely by other entities but have no structure of their own
  yet.

## Projection kinds this slice cannot demonstrate yet

`ContextMap` and `CausalityAndEventFlow` are implemented (issue #64) but stay
minimal for any RML 1.0 model, not specifically this one:

- RML 1.0 supports exactly one `context` declaration per workspace, so
  `ContextMap` — which shows cross-context relationships — always renders a
  single node with no edges until the language grows multi-context support.
- RML 1.0's `behaviour` grammar has no syntax for declaring published events
  or effects, so `CausalityAndEventFlow` — which walks `BehaviourDefinition.PublishedEvents`
  — always renders an empty graph until events are exposed to authors.

`Lifecycle`, `RuleDecision`, `BehaviourMap`, and `Structural` all render real,
non-trivial content for this workspace.

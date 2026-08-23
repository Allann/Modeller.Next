# Deferred from the legacy model

This slice ports a broad cross-section of `M:\Modeller\samples\child-care-old`
(domain evidence only, not a compatibility target) rather than every field of
every entity. What was simplified or left out, by area:

- **Child**: only core identity fields are ported. Medical records, consent,
  community support, additional-needs, school, and CCSS-confirmation
  relationships are out of scope.
- **Arrangement**: `Payee` (Account), `EndReason` (ArrangementEndReason), and
  `CRNHolder` (Adult) are not yet ported.
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
- **Adult**: core identity and contact fields are ported
  (`model/entities/adult.modeller`) — first name, last name, former name,
  date of birth, CRN, home phone, mobile phone, and email. Title, gender,
  ethnic background, languages, addresses, employment status, highest
  education received, and CCSS-confirmed-adult stay out of scope; those need
  their own reference entities.
- **User**: core identity and authentication fields are ported
  (`model/entities/user.modeller`) — user name, first name, last name,
  authentication source system, authentication source tenant identifier,
  and authentication user identifier. Organisation memberships stay out of
  scope; that needs its own reference entity.

## Projection kinds this slice cannot demonstrate yet

`ContextMap` and `CausalityAndEventFlow` are implemented (issue #64) but stay
minimal for any RML 1.0 model, not specifically this one:

- RML 1.0 now supports multiple `context` declarations per workspace and
  cross-context `import`s between them (issue #120), so `ContextMap` can show
  a real dependency edge. This sample stays single-context by choice — see
  the Studio Playground's child-care example for a version with a second
  "Centre Operations" context — so `ContextMap` still renders a single node
  with no edges here specifically, not because the language can't do more.
- RML 1.0's `behaviour` grammar has no syntax for declaring published events
  or effects, so `CausalityAndEventFlow` — which walks `BehaviourDefinition.PublishedEvents`
  — always renders an empty graph until events are exposed to authors.

`Lifecycle`, `RuleDecision`, `BehaviourMap`, and `Structural` all render real,
non-trivial content for this workspace.

## Entity ownership (aggregate root) is now ported

RML 1.0's `entity` grammar gained an `owner "<EntityName>"` clause (issue
#123), so the legacy `.key` file's `owner(...)` fact — e.g.
`M:\Modeller\samples\child-care-old\entities\Absence\Absence.key` states
`owner(Centre)` — has an equivalent again. `model/entities/absence.modeller`
declares `owner "Centre"`. Other ported entities have not been audited for
the same fact yet.

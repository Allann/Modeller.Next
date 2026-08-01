# Modeller

Modeller describes the durable meaning of a system independently of the
frameworks, storage engines, interfaces, and generators used to realise it.

## Language

**Acceptance scenario**:
A named, reviewed domain example whose inputs and expected observable meaning
demonstrate that an architectural contract is satisfied.
_Avoid_: Implementation test, sample data, demo

**Actor**:
A person, organisation, or external system that participates in a behaviour
through a named domain role. The role is independent of technical identity and
authentication.
_Avoid_: User, account, persona

**Authorization policy**:
A policy deciding whether an actor may attempt a behaviour for a domain subject.
It fails closed when permission cannot be determined.
_Avoid_: Authentication, requirement, guard

**Behaviour**:
One complete, externally meaningful thing a system can do. Commands, queries,
rules, outcomes, effects, and events describe aspects of a behaviour rather than
being interchangeable with it.
_Avoid_: Operation, endpoint, use case

**Bounded context**:
An independently versioned domain ownership scope with an explicit semantic
surface. It owns its concepts and controls their exports and imports without
implying a separate package, process, or deployment.
_Avoid_: Service, module, deployment unit

**Canonical trace**:
An immutable, deterministically ordered graph explaining the semantic evaluation
steps taken for one request. It excludes timestamps, durations, host data, and
other operational telemetry.
_Avoid_: Log, OpenTelemetry trace, stack trace

**Capability**:
An enduring ability or responsibility of a system, realised through one or more
behaviours. A capability groups behaviours by business purpose but is not itself
executable and does not prescribe organisation or implementation.
_Avoid_: Module, service, feature

**Classification**:
A typed conclusion selecting one value from a closed set of domain meanings. It
describes an evaluation result without changing state or causing effects.
_Avoid_: Status, outcome, enumeration

**Command**:
A request from an actor to perform a behaviour that may change domain state or
produce effects. It expresses intent, can be accepted or rejected, and is not
evidence that the requested behaviour occurred.
_Avoid_: Behaviour, event, instruction

**Compatibility fixture**:
An immutable historical artifact and its expected load, migration, or rejection
result, retained to prove a declared compatibility promise over time.
_Avoid_: Regenerated sample, current snapshot, backup

**Conclusion**:
The typed, explained result of evaluating a rule. A conclusion may be a truth,
classification, or value, but does not itself change domain state.
_Avoid_: Outcome, effect, return value

**Conformance fixture**:
A versioned, machine-readable input and independently authored expected semantic
observation used to compare implementations against the same contract.
_Avoid_: Golden implementation output, unit test, runtime snapshot

**Context package**:
The independently versioned persisted unit owned by one bounded context. It
contains that context's canonical semantic definitions and explicit imports and
exports, while layout and source provenance remain non-semantic companions.
_Avoid_: Project file, deployment package, federation snapshot

**Context version**:
The immutable semantic release version of one bounded context. It communicates
compatibility of that context's exported meaning independently of persistence
schema versions and resolved dependency versions.
_Avoid_: Schema version, snapshot version, latest

**Decision**:
A named composition of rules that resolves a domain question by selecting among
defined conclusions. It explains how the supplied facts led to its conclusion
and causes no effects.
_Avoid_: Behaviour, policy, decision record

**Decision table**:
A decision representation whose typed rows map explicit Fact combinations to
declared conclusions under one hit policy. Row order cannot become an implicit
default or change meaning where the hit policy does not define ordering.
_Avoid_: Spreadsheet, lookup table, separate rules engine

**Declared function**:
A pure, versioned domain calculation referenced explicitly by canonical rule
meaning when built-in expressions are insufficient. Its adapter receives only
typed arguments and cancellation and cannot introduce ambient information.
_Avoid_: Callback, script, external service

**Diagnostic**:
A structured report of an invalid definition, request, contract, or expected
technical failure. It is distinct from a finding that explains domain reasoning.
_Avoid_: Finding, conclusion, exception text

**Disclosure policy**:
An explicit rule controlling which protected facts, evidence, findings, and
trace details may appear in a projection for a named audience. It changes
visibility without changing the evaluation result.
_Avoid_: Authorization policy, redaction afterthought, trace level

**Diagram projection**:
A derived visual graph that reveals selected semantic concepts and relationships
without owning or changing their meaning. It is recreated from a semantic model
revision, a view definition, and optional layout state.
_Avoid_: Model, source of truth, visual schema

**Effect**:
A declared domain consequence owned by a behaviour. An effect may change state
or request an interaction, but is never produced directly by a rule or decision.
_Avoid_: Conclusion, outcome, side effect

**Edit operation**:
An explicit request produced by an editor interaction and classified as exactly
one semantic, view, layout, session, or invalid operation before anything is
changed. Spatial gestures alone never imply semantic intent.
_Avoid_: Graph mutation, gesture side effect, generic delete

**Entity**:
A domain concept with stable identity and continuity through changes in state.
Its meaning and lifecycle are independent of how or whether it is persisted.
_Avoid_: Record, table, document

**Event**:
An immutable statement that a domain-significant fact occurred. It may result
from a behaviour or report something that occurred externally, and is neither a
request nor an instruction.
_Avoid_: Command, notification, message

**Evaluation result**:
The immutable determined, indeterminate, invalid, or failed result of evaluating
one rule or decision against one request. Cancellation is control flow and is
never an evaluation result.
_Avoid_: Outcome, response, exception

**Evidence**:
Immutable provenance supplied with facts and referenced by findings. Evidence
supports an explanation but does not by itself establish that a source is
trustworthy.
_Avoid_: Fact, attachment, document

**Explanation**:
An audience-appropriate account projected from structured conclusions, findings,
evidence references, diagnostics, and optional canonical traces. It communicates
meaning without becoming a second evaluation result.
_Avoid_: Raw trace, free-text result, diagnostic dump

**Fact**:
A typed piece of domain information supplied to rule or decision evaluation as
known for that evaluation. Facts are inputs to reasoning, not mutable evaluation
state or untyped property bags.
_Avoid_: Variable, parameter, raw data

**Federation snapshot**:
An immutable, reproducible resolution of exact context-package versions,
content digests, imports, and exports. It is the authority consumed by
evaluation and generation, but is derived rather than directly authored.
_Avoid_: Context package, workspace, latest version

**Finding**:
An explained observation produced while evaluating a rule or decision that
supports, qualifies, or prevents a conclusion. A finding is not necessarily an
error.
_Avoid_: Diagnostic, exception, outcome

**Guard**:
A rule evaluated against current facts to determine whether a particular
behaviour or lifecycle transition is allowed now. It produces an explained
conclusion and no effects.
_Avoid_: Validation, authorization, condition

**Hit policy**:
The declared rule determining how matching decision-table rows produce a
conclusion. Every supported policy has explicit overlap, completeness, and
ordering semantics.
_Avoid_: Row order, default branch, engine setting

**Invariant**:
A rule that must hold for domain state to be valid before and after every
externally observable behaviour. A violated invariant prevents the proposed
state from becoming observable.
_Avoid_: Guard, validation rule, database constraint

**Layout state**:
Non-semantic presentation choices for one view, such as positions, sizes,
routing, orientation, and collapsed appearance. Removing it changes no domain
meaning and permits the diagram to be laid out again.
_Avoid_: Model state, ownership, relationship

**Lifecycle**:
The meaningful stages an entity may occupy and the permitted transitions among
them from creation to termination. Behaviours cause transitions; guards decide
whether a transition is currently allowed.
_Avoid_: Workflow, status, state

**Migration**:
An explicit, deterministic transformation from one declared schema or context
version to another. A schema migration preserves meaning; a model migration is
an authored change to meaning.
_Avoid_: Silent upgrade, loader fallback, data repair

**Outcome**:
The explicit domain result of a completed behaviour, including success,
rejection, or another defined result. It is distinct from the conclusions used
to reach it and the effects it authorises.
_Avoid_: Conclusion, effect, response

**Package digest**:
A deterministic digest of the exact persisted bytes of one context package. It
identifies packaging and provenance independently of the package's normalized
semantic meaning.
_Avoid_: Semantic digest, context version, file name

**Policy**:
A named rule or decision expressing a domain choice about what is permitted,
required, or entitled. Behaviours enforce policies; policies do not cause
effects themselves.
_Avoid_: Guard, procedure, configuration

**Projection element**:
A node or edge occurrence in one diagram projection, with a stable identity
within that view and references to zero or more semantic concepts. Repeated
occurrences never create additional semantic identities.
_Avoid_: Semantic concept, model element, canonical identity

**Query**:
A request from an actor to observe domain information without changing domain
state or producing domain effects. It returns an answer rather than evidence
that something occurred.
_Avoid_: Command, lookup, report

**Readable source**:
A versioned, author-facing representation that proposes canonical semantic
meaning and retains source provenance. It compiles into the authored model and
never becomes a parallel semantic authority.
_Avoid_: Canonical model, context package, executable script

**Requirement**:
A domain prerequisite that must be satisfied before a behaviour may proceed.
Missing information is distinct from an unsatisfied requirement.
_Avoid_: Guard, invariant, validation

**Rule**:
A named, reusable definition that evaluates typed facts and produces an
explained conclusion without changing domain state or causing effects. Rules
may compose other rules.
_Avoid_: Condition, validation, business logic

**Rule binding**:
An explicit semantic relationship stating why and how a governing concept uses
a reusable rule or decision. It maps available domain information to typed facts
and relevant conclusions to their governing purpose.
_Avoid_: Embedded expression, callback, rule copy

**Runtime plan**:
An immutable, disposable executable projection derived from a resolved semantic
snapshot and its relevant runtime versions. It is safe for concurrent reuse but
is never an authoritative model or persisted source definition.
_Avoid_: Runtime model, compiled source, authoritative executable

**Schema version**:
The version of a persisted representation's structural contract. It determines
which loader or migration is required but says nothing about domain meaning or
compatibility of a bounded context's exports.
_Avoid_: Context version, model version, product version

**Semantic digest**:
A deterministic digest of normalized semantic meaning. It excludes source
partitioning, formatting, layout, provenance, and other non-semantic packaging
details.
_Avoid_: File hash, package digest, version

**Semantic validation**:
An explicit, staged analysis of authored or resolved semantic meaning that
returns deterministic diagnostics without executing behaviours, repairing the
model, or deciding whether a domain action is currently allowed.
_Avoid_: Guard, invariant, rule evaluation, parser error

**Session state**:
Transient editor presentation such as selection, hover, viewport, open panels,
or an evaluation highlight. It changes neither semantic meaning, the view
definition, nor shared layout unless explicitly saved as layout.
_Avoid_: Layout state, view definition, model annotation

**Source provenance**:
Non-semantic information relating a stable semantic concept to one or more
versioned source artifacts and locations. It explains origin without determining
meaning or identity.
_Avoid_: Ownership, identity, semantic relationship

**State**:
The complete set of domain facts currently associated with an entity. A
lifecycle stage is one part of state, not a synonym for the whole of it.
_Avoid_: Status, lifecycle stage, persisted data

**Trace level**:
The requested amount of canonical evaluation detail: none, summary, or full.
It changes trace projection only and never changes conclusions or findings.
_Avoid_: Disclosure policy, log level, telemetry sampling

**Transition**:
A permitted change from one lifecycle stage to another, caused by successful
behaviour. It identifies its source and target stages, may be guarded, and may
result in events or other effects.
_Avoid_: Command, event, state change

**Transition guard**:
A guard owned by a lifecycle transition that determines whether that declared
transition is currently allowed. It validates an explicitly selected transition
rather than selecting among transitions.
_Avoid_: Transition selector, requirement, invariant

**View definition**:
A persisted, non-semantic selection of what a diagram projection reveals. It
identifies a view kind, scope, roots, filters, inclusions, exclusions, and
expansion choices independently of visual layout.
_Avoid_: Semantic model, diagram image, layout state

**Workflow**:
A behaviour that coordinates multiple behaviours toward a domain outcome,
often across time and in response to outcomes or events. Its coordination and
progress are explicit domain meaning rather than a sequence of technical calls.
_Avoid_: Pipeline, script, orchestration

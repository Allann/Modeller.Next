# Modeller

Modeller describes the durable meaning of a system independently of the
frameworks, storage engines, interfaces, and generators used to realise it.

## Language

**Actor**:
A person, organisation, or external system that participates in a behaviour
through a named domain role. The role is independent of technical identity and
authentication.
_Avoid_: User, account, persona

**Behaviour**:
One complete, externally meaningful thing a system can do. Commands, queries,
rules, outcomes, effects, and events describe aspects of a behaviour rather than
being interchangeable with it.
_Avoid_: Operation, endpoint, use case

**Capability**:
An enduring ability or responsibility of a system, realised through one or more
behaviours. A capability groups behaviours by business purpose but is not itself
executable and does not prescribe organisation or implementation.
_Avoid_: Module, service, feature

**Command**:
A request from an actor to perform a behaviour that may change domain state or
produce effects. It expresses intent, can be accepted or rejected, and is not
evidence that the requested behaviour occurred.
_Avoid_: Behaviour, event, instruction

**Conclusion**:
The typed, explained result of evaluating a rule. A conclusion may be a truth,
classification, or value, but does not itself change domain state.
_Avoid_: Outcome, effect, return value

**Decision**:
A named composition of rules that resolves a domain question by selecting among
defined conclusions. It explains how the supplied facts led to its conclusion
and causes no effects.
_Avoid_: Behaviour, policy, decision record

**Effect**:
A declared domain consequence owned by a behaviour. An effect may change state
or request an interaction, but is never produced directly by a rule or decision.
_Avoid_: Conclusion, outcome, side effect

**Entity**:
A domain concept with stable identity and continuity through changes in state.
Its meaning and lifecycle are independent of how or whether it is persisted.
_Avoid_: Record, table, document

**Event**:
An immutable statement that a domain-significant fact occurred. It may result
from a behaviour or report something that occurred externally, and is neither a
request nor an instruction.
_Avoid_: Command, notification, message

**Evidence**:
Immutable provenance supplied with facts and referenced by findings. Evidence
supports an explanation but does not by itself establish that a source is
trustworthy.
_Avoid_: Fact, attachment, document

**Fact**:
A typed piece of domain information supplied to rule or decision evaluation as
known for that evaluation. Facts are inputs to reasoning, not mutable evaluation
state or untyped property bags.
_Avoid_: Variable, parameter, raw data

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

**Invariant**:
A rule that must hold for domain state to be valid before and after every
externally observable behaviour. A violated invariant prevents the proposed
state from becoming observable.
_Avoid_: Guard, validation rule, database constraint

**Lifecycle**:
The meaningful stages an entity may occupy and the permitted transitions among
them from creation to termination. Behaviours cause transitions; guards decide
whether a transition is currently allowed.
_Avoid_: Workflow, status, state

**Outcome**:
The explicit domain result of a completed behaviour, including success,
rejection, or another defined result. It is distinct from the conclusions used
to reach it and the effects it authorises.
_Avoid_: Conclusion, effect, response

**Policy**:
A named rule or decision expressing a domain choice about what is permitted,
required, or entitled. Behaviours enforce policies; policies do not cause
effects themselves.
_Avoid_: Guard, procedure, configuration

**Query**:
A request from an actor to observe domain information without changing domain
state or producing domain effects. It returns an answer rather than evidence
that something occurred.
_Avoid_: Command, lookup, report

**Rule**:
A named, reusable definition that evaluates typed facts and produces an
explained conclusion without changing domain state or causing effects. Rules
may compose other rules.
_Avoid_: Condition, validation, business logic

**State**:
The complete set of domain facts currently associated with an entity. A
lifecycle stage is one part of state, not a synonym for the whole of it.
_Avoid_: Status, lifecycle stage, persisted data

**Transition**:
A permitted change from one lifecycle stage to another, caused by successful
behaviour. It identifies its source and target stages, may be guarded, and may
result in events or other effects.
_Avoid_: Command, event, state change

**Workflow**:
A behaviour that coordinates multiple behaviours toward a domain outcome,
often across time and in response to outcomes or events. Its coordination and
progress are explicit domain meaning rather than a sequence of technical calls.
_Avoid_: Pipeline, script, orchestration

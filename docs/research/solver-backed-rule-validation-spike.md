---
title: Solver-backed rule validation spike
description: Result of the narrow Truth/And and Child Care SMT projection spike.
---

Date: 2026-08-17

## Decision question

Can an SMT solver find useful model defects or produce useful witnesses beyond the reference interpreter and conformance fixtures, at an acceptable trust and maintenance cost?

## Result

**Narrow adoption is justified for optional authoring and bind-time checks.** The spike found and explained decision-row overlap, a coverage gap, an unreachable conclusion, contradictory named facts, and a semantic change. It returned concrete Boolean witnesses for satisfiable queries and stable semantic IDs in conflict cores. Exhaustive comparison over all four Child Care Truth assignments agreed with the reference interpreter.

Do not add the solver to the production evaluation path. Do not publish a general proof-artifact contract yet. The useful result is the counterexample and defect-detection seam. A maintained translator and cross-check fixtures must exist before this seam moves out of a prototype.

## Prototype boundary

The prototype reads the real `RuleDefinition`, `DecisionDefinition`, and Child Care context-package fixture. It translates only:

- Truth facts;
- canonical `And` expressions;
- Truth decision-row conditions, including `Any`; and
- the current Unique decision table.

All other expression syntax fails closed with `solver.translation.unsupported-expression`. No approximation or fallback occurs. The code is under `prototypes/` and has no reference from `Modeller.Rules`.

The reference interpreter remains the semantic authority. The test harness enumerates every complete Boolean assignment, evaluates the real runtime, and compares its conclusion with solver satisfiability.

## Queries and artefacts

The spike separates fact consistency from claim and refutation checks. It represents `sat`, `unsat`, `unknown`, solver failure, and cancellation as different values. An inconsistent fact set produces `inconsistent-or-invalid`; it cannot produce both an entailed and contradicted domain conclusion.

Each answer contains:

- a canonical-query SHA-256 digest;
- a sorted model keyed by stable fact ID for `sat`;
- a sorted named conflict core for `unsat`; and
- a safe detail category for `unknown`, failure, or cancellation.

A future public solver artefact can add the semantic snapshot digest, canonical-language version, translator and solver versions and options, query kind, referenced conclusion/rule/row IDs, resource budget, and disclosure classification. Keep it separate from `CanonicalTrace`. A conflict core is sufficient to reproduce a conflict, but it is not a minimal proof.

## Reproducibility and resource limits

The package is pinned to `Microsoft.Z3` 4.12.2. The context enables models and unsat cores. Assertions and model fields are sorted. Repeated identical queries are structurally identical in the tests.

The default per-query bounds are a deterministic Z3 resource limit of 100,000 and a 2,000 ms operational timeout. A resource limit of 1 produces `unknown`, not a domain conclusion. The wall-clock timeout is a safety stop and is not deterministic evidence. The resource limit, solver build, options, translator version, and query digest must be retained for reproducibility.

Eight focused tests complete in less than one second on the spike workstation after build. This is sufficient for authoring-time checks in the two-fact slice. It is not evidence for larger expressions or tables. Measure those fragments before support expands.

## Dependency and packaging

The NuGet dependency includes native solver binaries and increases restore and distribution size. Version 4.12.2 is the stable package available from NuGet, while newer upstream Z3 releases publish packages as GitHub release assets. This split increases update and supply-chain work. Cross-platform native packaging must be verified in CI before adoption. The project must pin the package, retain its resolved hash, and test each supported runtime identifier.

An external `cvc5` process would isolate native loading and make SMT-LIB retention simple, but it adds executable discovery, process control, sandboxing, and deployment work. The in-process Z3 API made the spike small. It is not yet a packaging recommendation.

## Security and disclosure

Models and conflict cores can disclose supplied fact values and the stable IDs of protected policy elements. Treat them as audit artefacts. Apply disclosure policy before storage or presentation. Do not put raw fact values, policy text, solver exception messages, host paths, timestamps, or environment data in a canonical artefact.

The translator is inside the trust boundary because a translation defect can produce a valid solver answer to the wrong formula. Keep the translator pure, versioned, small, and exhaustively cross-checked. The solver is an independent checker, not an independent formalisation.

## Maintenance cost

The Boolean slice is small and readable. The cost rises with each canonical type and operator because Modeller must define exact mappings for missing information, type rules, functions, and resource use. Do not add Number, Date, Text, declared functions, or partial information until each fragment has exhaustive or generated conformance evidence.

## Recommendation

Adopt a narrow, optional validation seam after a production-quality translator contract exists:

1. Start with decision overlap, coverage, reachability, and semantic-change witnesses.
2. Keep solver results out of runtime conclusions and `CanonicalTrace`.
3. Fail closed for unsupported syntax and map `unknown`, failure, and cancellation to operational results.
4. Retain named assertions and apply disclosure rules to models and cores.
5. Require interpreter/solver cross-check fixtures for every supported expression fragment.
6. Defer the AI claim-verification seam. Its later contract must accept a pinned snapshot, typed facts with provenance, and a structured claim. LLM output stays outside the proof boundary.

The solver adds useful assurance because it produces witnesses for properties that ordinary example fixtures do not cover. The benefit justifies the translator only while the supported fragment stays narrow and authoring-time only.

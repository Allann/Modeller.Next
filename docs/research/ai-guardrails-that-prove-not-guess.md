# AI guardrails that prove, not guess

Date: 2026-08-05

## Question

What pattern does CommBank Technology's [AI Guardrails That Prove, Not Guess](https://medium.com/@CommBankTechnology/ai-guardrails-that-prove-not-guess-8b9372f8d4be) propose, how strong are its claims, and what parts could Modeller adopt?

## Short answer

The article proposes a useful separation of responsibilities: an LLM translates human language into a small typed logical problem, while an SMT solver—not another LLM—decides whether a claim follows from versioned policy and facts. That is closely aligned with Modeller's existing canonical, typed, deterministic rules runtime. Modeller already has most of the valuable foundation: stable semantic IDs, typed facts, versioned definitions, deterministic evaluation, explicit indeterminacy, structured findings/evidence, and canonical traces.

The best adoption is therefore **not to replace Modeller evaluation with an SMT solver**. It is to add an optional proof/checking projection for the expression fragment that can be translated faithfully, and to use solver queries at authoring/bind time to find contradictions, gaps, overlaps, unreachable branches, and counterexamples. A later AI-facing claim-verification adapter could map extracted facts and claims into a pinned Modeller snapshot, but the LLM translation steps must remain outside the trusted proof boundary.

## The article's pattern

The article calls the pattern **AI+AR** (AI plus automated reasoning):

1. Humans write policy in ordinary language.
2. An LLM compiles it into typed variables and formal logic.
3. The compiled policy is stored as reviewable, versioned JSON beside the original text.
4. For a live request, an LLM extracts facts and the claim made by an AI answer.
5. A solver checks both `F ∧ φ ∧ c` and `F ∧ φ ∧ ¬c`, where `F` is the facts, `φ` the policy, and `c` the claim.
6. The response is passed when the claim is entailed, held when contradicted, and routed for review when the policy permits both outcomes.

The intended value is a deterministic semantic verdict with rule-level evidence, rather than a probabilistic acceptability score from an LLM judge. The article is careful about the central limitation: the result is exact only relative to the formalisation. A wrongly compiled policy or wrongly extracted fact set produces a correct answer to the wrong question.

The underlying mechanism is standard SMT. SMT-LIB defines common languages and semantics for satisfiability-modulo-theories solvers ([SMT-LIB overview](https://smt-lib.org/)); its current logic catalogue defines QF_LIA as unquantified linear integer arithmetic ([SMT-LIB logics](https://smt-lib.org/logics.shtml)). cvc5 is an open-source SMT solver supporting multiple theories and their combinations ([cvc5 documentation](https://cvc5.github.io/docs/latest/)).

## What the proof does—and does not—establish

For consistent `F ∧ φ`, the two checks have the intended reading:

| `F ∧ φ ∧ c` | `F ∧ φ ∧ ¬c` | Meaning |
| --- | --- | --- |
| SAT | UNSAT | `c` is entailed (proved relative to `F` and `φ`) |
| UNSAT | SAT | `c` is contradicted/refuted |
| SAT | SAT | both outcomes remain possible; policy/facts under-specify the claim |
| UNSAT | UNSAT | `F ∧ φ` is itself inconsistent; no claim verdict is valid |

That fourth row matters. The article says the pair partitions every claim into exactly three verdicts, but that is true only after separately establishing that `F ∧ φ` is satisfiable. Modeller should expose the fourth row as **Invalid**, not force it into “proved” or “refuted.” This maps naturally to Modeller's existing `InvalidResult`.

There are three further precision points:

- **An unsat core is not automatically a proof or a minimal explanation.** It is a subset of assertions sufficient to remain unsatisfiable. cvc5 supports named unsat cores, but its own tutorial says cores are not guaranteed to be minimal. cvc5 offers a locally-minimal mode at additional cost, not a guarantee of a globally smallest core ([cvc5 interfaces](https://cvc5.github.io/blog/2024/04/15/interfaces-for-understanding-cvc5.html), [solver outputs](https://cvc5.github.io/tutorials/beginners/outputs.html)). If Modeller presents a core, call it a conflict core and map its named assertions to rule/fact IDs; do not call it “the single violated rule” unless that property has been established.
- **Proof objects are stronger than cores.** cvc5 distinguishes a proof—a structured derivation that may be independently checked—from an unsat core, which helps locate relevant assertions. Its tutorial documents both capabilities ([cvc5 solver outputs](https://cvc5.github.io/tutorials/beginners/outputs.html)). An evidence contract should identify whether an artefact is a verdict, model/counterexample, core, or independently checkable proof.
- **Termination does not mean every production call returns SAT or UNSAT.** QF_LIA is decidable in the mathematical sense, but production systems impose time/resource budgets. cvc5 returns `unknown` when an internal per-check limit is exhausted ([cvc5 resource limits](https://cvc5.github.io/docs/cvc5-1.3.3/resource-limits.html)); SMT-LIB 2.7 also standardises `unknown` under reproducible resource limits ([SMT-LIB 2.7 standard](https://smt-lib.org/papers/smt-lib-reference-v2.7-r2025-07-07.pdf)). Modeller should map `unknown`, timeout, cancellation, and solver failure to operational **Failed**/cancellation outcomes, never to a domain conclusion.

The worked SMT-LIB excerpt in the article also asserts the policy conditions and facts but does not visibly assert a distinct claim proposition. It demonstrates that the request violates an eligibility-condition conjunction; a reusable implementation should model the conclusion explicitly (for example, `allowed ↔ policy_conditions`) and test `allowed` and `¬allowed`, otherwise policy consistency and claim verification can be conflated.

## Similarities already present in Modeller

Modeller's accepted architecture is substantially the same trust model, without depending on SMT:

| Article concept | Existing Modeller concept |
| --- | --- |
| Typed variables and ground facts | Typed facts keyed by stable semantic fact IDs; no silent coercion ([rule evaluation interface](/docs/architecture/decisions/rule-evaluation-interface)) |
| Versioned formal policy | Versioned canonical, statically typed expression representation ([canonical rule expressions](/docs/architecture/decisions/canonical-rule-expressions)) |
| Deterministic verdict | Snapshot, facts, evaluator and function versions determine the result and canonical trace ([rule evaluation interface](/docs/architecture/decisions/rule-evaluation-interface)) |
| Ambiguous/under-specified | `IndeterminateResult` with the exact missing facts that prevent a conclusion |
| Inconsistent or malformed input | `InvalidResult` with structured diagnostics |
| Rule-level explanation | Structured findings with rule, decision, fact and evidence references; optional canonical trace |
| Versioned/replayable decision basis | Resolved snapshot identity, evaluator/function versions, immutable request, stable ordering |
| Solver resource exhaustion | Stable `FailedResult` for work-budget exhaustion; cancellation remains separate control flow |
| Human wording outside semantics | Explanation is a projection of structured findings, not new semantic evidence |

Modeller is already stronger in several areas the article only sketches: it distinguishes missing, invalid, failed, and cancelled evaluation; separates domain findings from diagnostics; records evidence provenance and disclosure policy; defines deterministic semantic work budgets; and requires conformance across runtime implementations ([reusable runtime architecture](/docs/architecture/decisions/reusable-rules-runtime), [rules runtime reference](/docs/reference/rules-runtime)).

## Actionable adoption ideas

### 1. Add solver-backed authoring checks first

Translate only a declared, semantics-preserving subset of canonical expressions to SMT. At bind/validation time, use it to detect:

- contradictory rules or impossible decision-table rows;
- overlapping rows (already rejected for the initial Unique policy, but a solver can generalise the check);
- uncovered input regions and counterexample assignments;
- conclusions or branches that are unreachable;
- whether one rule implies or subsumes another; and
- whether a proposed rule change introduces a new semantic difference, with a witness assignment.

This is lower risk than runtime gating because definitions are reviewed once, counterexamples are inspectable, and the existing reference interpreter remains semantic authority.

### 2. Define a precise proof artefact contract

If solver evidence becomes public, represent it as structured data, for example:

- semantic snapshot digest and canonical-language version;
- translator ID/version and solver ID/version/options;
- query kind (`consistency`, `entailment`, `refutation`, `coverage`, `equivalence`);
- stable claim/conclusion ID;
- referenced rule and fact IDs;
- result (`sat`, `unsat`, `unknown`);
- a model/counterexample for SAT, or named conflict core/proof reference for UNSAT;
- deterministic resource budget and disclosure classification; and
- digest of the canonical query, with optional retained SMT-LIB for audit.

Keep this distinct from the existing canonical trace: a trace explains how the Modeller evaluator reached a result; a solver artefact independently checks a formal proposition about the same snapshot.

### 3. Make the translation trust boundary explicit

The canonical-to-SMT adapter must be deterministic, versioned, pure, and covered by bidirectional conformance fixtures. For generated assignments in the supported fragment, compare the reference interpreter's result with the solver encoding. Unsupported expressions must fail closed with a structured diagnostic—never approximate or silently fall back.

Natural-language-to-policy compilation should produce a **draft change**, not an authoritative policy. Require human review of the canonical diff plus generated counterexamples/equivalence checks before publishing a new snapshot. Likewise, AI-extracted facts should carry provenance/evidence and confidence outside the semantic value; uncertainty should become missing information or review, not a guessed fact.

### 4. Add an AI claim-verification seam later

For AI systems answering questions governed by a Modeller snapshot:

1. Pin the exact snapshot and claim schema.
2. Map the request into typed Modeller facts with provenance.
3. Evaluate the target rule/decision using the existing runtime.
4. Compare the AI's structured claim with the typed Modeller conclusion.
5. Pass matching determined claims; repair/refuse contradictions; route indeterminate or invalid inputs to review; treat failures/cancellation operationally.

For ordinary Modeller conclusions, this direct comparison is simpler than issuing two solver checks. Solver entailment becomes valuable when a claim is a richer proposition than one evaluated conclusion, when checking policy-wide properties, or when an independent checker is required.

### 5. Run a narrow spike before an architectural commitment

Use the current Truth/`And` slice and the Child Care decision fixture to prove:

1. faithful canonical-to-SMT translation;
2. consistent four-way semantic handling plus `unknown`/failure;
3. stable mapping from named assertions to semantic IDs;
4. counterexamples for gaps/overlaps and readable diagnostics;
5. cross-checks against exhaustive reference-interpreter assignments; and
6. bounded performance and reproducibility under pinned solver options.

The spike should not put cvc5 on the production evaluation path. Its decision point is whether solver-backed validation and proof artefacts provide enough additional assurance to justify a maintained translation and solver dependency.

## Recommendation

Adopt the article's principle—**probabilistic translation, deterministic verification, explicit audit artefacts**—but ground it in Modeller's existing semantics. Start with solver-backed validation and counterexample generation. Preserve the canonical interpreter as authority, model inconsistency and resource exhaustion explicitly, and distinguish cores from proofs. Only after the translation is demonstrated equivalent should Modeller expose solver artefacts or an AI claim-verification adapter.

This gives Modeller something stronger than the article's toy pipeline: the LLM can assist authoring and fact extraction, while a versioned domain model, a deterministic runtime, and optionally an independent solver each have a narrow, reviewable role.

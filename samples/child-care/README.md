# Child Care sample

This is Modeller's canonical executable sample. It uses the business-facing
Readable Modelling Language and grows one accepted Child Care capability at a
time. The legacy sample is domain evidence only; it is not an import source or
compatibility target.

## Current slice

The model now covers two capability areas built around a shared care-booking
graph, so it stands as both the decision-heavy business-domain flagship and
the multiple-downstream-projections flagship.

**ACCS determination** — the original slice:

- Draft and Submitted lifecycle stages;
- active-enrolment and supporting-evidence Facts;
- the Determine ACCS eligibility Rule;
- submission and rejection Outcomes; and
- the guarded transition produced by a successful submission.

**Booking, attendance, and billing** — a broader sweep ported from the legacy
`M:\Modeller\samples\child-care-old` sample (see `gaps.md` for what was
simplified or left out):

- a Booking lifecycle (Planned → Attending → Attended/Absent → Billed) tied
  to its Routine/Casual booking sessions, Arrangement, Session, Room, and
  Child structure;
- the Determine absence chargeability Rule and Record absence Behaviour;
- the Determine charge amount Rule and Run billing for booking Behaviour,
  chained off a Booking's attendance outcome;
- Centre/Room/Room age group structure for a richer entity graph; and
- Account, Family account, Charge, Charge type, and Charge reason for the
  billing side.

Compile and validate the complete declared model with a generation preview:

```powershell
dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
```

## Structure

- `.modeller/config.json` declares every generation input and the owned-output manifest; `.modeller/identities.json` is tooling-owned canonical identity metadata.
- `model/` contains small RML files organised by semantic concept.
- `templates/csharp/` is the C# pack catalogue; `domain-project/` is the reusable pinned pack selected by this workspace, and `api-project/` is a deeper Minimal-API pack used for realistic generation testing.
- `templates/python/` is the Python pack catalogue; `api-project/` is a FastAPI pack that mirrors `templates/csharp/api-project/` scope-for-scope.
- `expected/` contains the deterministic golden output.
- `generated/` contains the current manifest-owned output.
- `gaps.md` records what was simplified or left out when porting from the
  legacy sample, and where the six diagram projection kinds stand for this
  workspace.

## Projections

List the available roots for any of the six diagram projection kinds, then
project one:

```powershell
dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural
dotnet run --project src/Modeller.Cli -- project --workspace samples/child-care --view Structural --root <id>
```

`Lifecycle`, `RuleDecision`, `BehaviourMap`, and `Structural` all render real,
non-trivial content for this workspace. `ContextMap` and
`CausalityAndEventFlow` currently render minimally for any RML 1.0 model —
see `gaps.md` for why.

## Acceptance destination

The sample is complete as a first usable vertical slice when these commands
work without hand-assembled planning requests:

```powershell
modeller generate --workspace samples/child-care --dry-run
modeller generate --workspace samples/child-care
dotnet build samples/child-care/generated/ChildCare.slnx
```

Run generation a second time to confirm that every artifact is reported as
`Unchanged`.

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
- the Determine absence chargeability Rule and Record absence Behaviour,
  with a distinct non-chargeable reason recorded separately from an
  absence's general absence reason;
- the Determine charge amount Rule and Run billing for booking Behaviour,
  chained off a Booking's attendance outcome;
- an Arrangement records its required payee Account, optional end reason,
  and optional CRN-holding Adult;
- an Enrolment identifies one Child, is owned by its Centre, and groups the
  child's Arrangements and reusable Enrolment tags; each Arrangement retains
  its payee Account;
- a Centre-owned Waitlist identifies one Child and records its fortnightly
  requested-care pattern, optional preferred Room, and optional end reason;
  it remains separate from Booking and Session;
- a Child records reusable community-support, specialised-support, and
  consent lists; optional School details; its Medical record and additional
  needs; and the child's CCSS-confirmed details;
- catalogue-backed Centre service offerings, weekly operating hours, service
  care type, optional ACN, coordinates, and organisational Structure nodes;
- Centre addresses select a reusable State, while Rooms belong to their Centre
  and record an optional nickname and auditable status history; and
- Adults select reusable title, gender, ethnic background, language,
  employment, and education references; own typed addresses that reuse State;
  and can identify optional government-confirmed adult details; and
- Centre/Room/Room age group structure for a richer entity graph; and
- Account, Family account, Charge, Charge type, and Charge reason for the
  billing side.

**Family and related adults** — the family care and responsibility graph:

- a Family groups its Children and family-specific Related adults, records an
  optional name and origin, and owns one Family account;
- each Related adult links one Adult to a relationship type, display priority,
  and authorisations without changing the Adult identity;
- ranked Family account holders link financial responsibility to Adults; and
- each Enrolment identifies its Family while each Arrangement retains its
  direct payee Account.

**Government subsidy reporting** — a bounded reporting path:

- a government enrolment occurrence connects one Arrangement to the Child's
  CCSS-confirmed details and records government and visible stages;
- separate readiness rules require confirmed child details before an
  occurrence and an active occurrence plus a delivered Booking before a report;
- a Weekly session report groups delivered Bookings for one week and moves
  from report Draft to report Submitted through a guarded behaviour;
- a returned Weekly subsidy result records weekly totals and groups complete
  per-session entitlements, including an optional nil-or-partial reason; and
- ACCS Arrangements reuse this path after the existing ACCS eligibility and
  determination workflow. The reporting path does not duplicate that workflow.

**Workforce and organisation access** — a fail-closed authorisation boundary:

- Users can belong to more than one Organisation, while each Employee and Role
  belongs to one Organisation;
- Roles grant named Rights through Rights groups;
- dated Security assignments join one User, Role, and exact Structure node; and
- access requires a current, organisation-consistent assignment, membership,
  an exact node match, and the required right. The model does not infer access
  through parent or child structure nodes and does not model credentials.

**User notifications** — a bounded user-message workflow:

- an Organisation owns each User notification and each notification identifies
  one User;
- the notification records subject, description, optional URL, audience type,
  and current status;
- User notification type keeps the legacy User, Centre, and Provider audience
  reference values; and
- the bounded workflow creates a User-audience notification, then moves it
  from New to Viewed to Completed. Delivery channels, retry queues, templates,
  external providers, and read receipts stay outside this sample slice.

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

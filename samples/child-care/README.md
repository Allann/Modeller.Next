# Child Care sample

This is Modeller's canonical executable sample. It uses the business-facing
Readable Modelling Language and grows one accepted Child Care capability at a
time. The legacy sample is domain evidence only; it is not an import source or
compatibility target.

## Current slice

The first slice models an ACCS determination application:

- Draft and Submitted lifecycle stages;
- active-enrolment and supporting-evidence Facts;
- the Determine ACCS eligibility Rule;
- submission and rejection Outcomes; and
- the guarded transition produced by a successful submission.

Compile and validate the complete declared model with a generation preview:

```powershell
dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
```

## Structure

- `.modeller/config.json` declares every generation input and the owned-output manifest; `.modeller/identities.json` is tooling-owned canonical identity metadata.
- `model/` contains small RML files organised by semantic concept.
- `templates/csharp/` is the C# pack catalogue; `domain-project/` is the reusable pinned pack selected by this workspace.
- `expected/` contains the deterministic golden output.
- `generated/` contains the current manifest-owned output.

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

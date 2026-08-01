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

Validate it from the repository root:

```powershell
dotnet run --project src/Modeller.Cli -- validate samples/child-care/model/accs-eligibility.modeller
```

## Structure

- `.modeller/config.json` is the current minimal workspace configuration.
- `model/` contains the current RML source.
- `templates/csharp/` will contain the first validated C# template pack.
- `expected/` describes and later stores deterministic acceptance output.
- `generated/` is reserved for CLI output and is not yet checked in.

## Acceptance destination

The sample is complete as a first usable vertical slice when these commands
work without hand-assembled planning requests:

```powershell
modeller validate samples/child-care/model/accs-eligibility.modeller
modeller generate --workspace samples/child-care --dry-run
modeller generate --workspace samples/child-care
dotnet build samples/child-care/generated/ChildCare.slnx
```

The last three commands describe the intended product workflow and are not yet
implemented.

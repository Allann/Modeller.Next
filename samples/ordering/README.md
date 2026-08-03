# Ordering sample

This is Modeller's simple, from-scratch intro sample — a small, familiar
workflow meant to be understood at a glance before a visitor takes on the
depth of the Child Care sample. It uses the same business-facing Readable
Modelling Language and grows one accepted Ordering capability at a time.

## Current slice

The first slice models an order's path to being placed:

- Draft and Placed lifecycle stages;
- a payment-confirmed Fact;
- the Determine order readiness Rule;
- placed and rejected Outcomes; and
- the guarded transition produced by a successful placement.

Compile and validate the complete declared model with a generation preview:

```powershell
dotnet run --project src/Modeller.Cli -- generate --workspace samples/ordering --dry-run
```

## Structure

- `.modeller/config.json` declares every generation input and the owned-output manifest; `.modeller/identities.json` is tooling-owned canonical identity metadata.
- `model/` contains small RML files organised by semantic concept.
- `templates/csharp/domain-project/` is the reusable pinned C# pack selected by this workspace (mirrors `samples/child-care`'s pack of the same name).

## Acceptance destination

The sample is complete as a first usable vertical slice when these commands
work without hand-assembled planning requests:

```powershell
modeller generate --workspace samples/ordering --dry-run
modeller generate --workspace samples/ordering
dotnet build samples/ordering/generated/Ordering.slnx
```

Run generation a second time to confirm that every artifact is reported as
`Unchanged`.

## Next steps

This slice is intentionally minimal. Deepening it (e.g. adding a Fulfilled
stage, an order line item, or a second rule) is follow-up content work, not
part of this scaffold.

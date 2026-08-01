# Modeller successor migration

The public product name remains **Modeller**. `Modeller.Next` is only a temporary
label used while the successor is reconstructed.

## Legacy baseline

Captured on 2026-08-01 from `M:\Modeller`:

- Remote: `https://github.com/CSharp-Catalyst/Modeller.git`
- Local head: `fd9cbc1` (`Removed ai-dev`)
- Remote `origin/main`: `8458981`
- The local branch is two commits ahead of `origin/main`.
- Untracked paths exist at `.scratch/`, `docs/Architecture101.md`, and `samples/modeller/.modeller/`.

The legacy checkout must not be renamed, deleted, or archived until those local
changes have been deliberately retained or discarded by their owner.

GitHub reported the `CSharp-Catalyst` organisation as being deleted when the
successor repository was created. The temporary successor therefore lives under
the active `Allann` account.

## Reconstruction rule

Legacy code is evidence of behaviour, not the design authority for the new
implementation. Each retained capability must be expressed as a Wayfinder issue,
given an explicit module interface, and verified through tests at that seam.

## Replacement gates

- Canonical product and architecture documentation is present here.
- Every retained legacy capability is represented in the migration inventory.
- New modules satisfy their documented interfaces and acceptance tests.
- Package, release, and documentation links point to the successor.
- All unpublished legacy work has been resolved.
- The legacy repository README redirects readers to the successor.

Only after every gate passes should the old repository be archived and this
repository be renamed from `Modeller.Next` to `Modeller`.

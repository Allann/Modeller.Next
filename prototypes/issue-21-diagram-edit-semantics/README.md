# PROTOTYPE — issue #21 diagram edit semantics

Question: across the six proposed initial diagram views, can every visual edit
be classified unambiguously as a semantic model operation, non-semantic view or
layout state, or an invalid operation?

This is a throwaway terminal prototype. It uses Child Care examples and keeps
all state in memory.

## Verdict

The six views can share one edit-classification contract with four outcomes:
semantic model operation, non-semantic view operation, layout operation, or
invalid operation. The prototype confirmed that view inclusion and geometry can
change independently of the semantic revision.

The critical interaction constraint is that **remove from view** and **delete
from model** must be separate named commands. A generic Delete gesture is
ambiguous and must not mutate the semantic model.

Run from the repository root:

```powershell
npm run prototype:diagram-edits
```

---
title: Start a Discovery Session
description: Open an Initiative at modeller.website and run Discover, Frame, and Shape with a Domain Expert.
---

# Start a Discovery Session

Discovery is where a Modeller Initiative begins, and it happens in the browser at
**[modeller.website](https://modeller.website)** — no install, no account, nothing
to configure. This page is the deliberate path from "someone asked us to build
something" to a recorded Initiative you can work through with a Domain Expert.

You do not need the CLI, the VS Code extension, or a model to do any of this.
Those belong to [System Design](/docs/getting-started/quick-start), which an
Initiative only reaches if it selects a technology intervention.

## Before you start

Have two things ready:

- **The original change request, in the words it arrived in.** Do not tidy it
  into a problem statement — the Initiative deliberately keeps the request and
  the problem separate. "Build us a new approval system" is a perfectly good
  entry.
- **A Domain Expert.** Someone who lives with the situation and can answer
  questions about it. You will send them a link.

You will facilitate: guiding the session, deciding which questions to ask, and
accepting what becomes part of the record. See
[Initiatives](/docs/concepts/initiatives) for what each role is responsible for.

## 1. Open modeller.website and create the Initiative

Go to **[modeller.website](https://modeller.website)** and fill in the form on
the landing page:

| Field | What to put in it |
| --- | --- |
| Original change request | The request verbatim |
| Facilitator name | You |
| Domain Expert name | The person you are about to invite |

Select **Start the Initiative**. You land on the Facilitator cockpit at
`modeller.website/initiative/<id>`.

> **Bookmark that URL.** There are no accounts, so the link is the only way back
> into your Initiative.

## 2. Send the Domain Expert their link

The cockpit shows a **Domain Expert link** (`.../initiative/<id>/respond`).
Send it to them however you normally would.

That view is deliberately narrow: it shows the change request and the one
question currently waiting for them. Questions you are still drafting, gate
findings, and the intervention comparison stay on your side of the session.

## 3. Work the question loop through Discover and Frame

The cockpit's **Discover & Frame** section drives the whole conversation with one
repeating loop:

1. Pick the field you are trying to fill (problem statement, affected people,
   desired outcomes, constraints, and so on) and propose a question against it.
   Leave the question text blank to have the configured agent advisor suggest
   one — optional, and never required.
2. **Send to Domain Expert**, or reject the question if it is not worth asking.
3. They answer in their view; the answer arrives back in the cockpit.
4. **Accept** the response to draft it into the structured field.

The phase indicator moves through Capture Request, Clarify Problem, Identify
Impact, Define Outcomes, and Set Boundaries as those fields fill in. Both views
update live, so you can run this in a call or asynchronously over days.

## 4. Check the Discovery Gate, then Shape

The **Discovery Gate** reports what is still thin — a missing outcome, an
unevidenced problem. It is advisory: it never blocks you, and you can record an
override with a reason and move on.

In **Shape**, compare interventions across the fixed taxonomy (process, people,
organisation, policy, information, technology, experiment, and no action). Select
as many as the outcome genuinely needs, and mark for each whether it continues
into a design workspace. The Shape Gate is advisory in exactly the same way.

## 5. Finalize, or continue into System Design

Finalizing records the Initiative as it stands, including whether it closed
cleanly or with open gate findings. You can reopen it later.

If you selected a technology intervention that continues into design, that is
the point where [System Design](/docs/getting-started/quick-start) starts — and
the reason for the design travels with it.

## Related

- [Worked Initiative example: delayed building variations](/docs/guides/building-variation-initiative) — follow a complete Facilitator and Domain Expert conversation.
- [Initiatives](/docs/concepts/initiatives) — the concepts and roles behind the session.
- [The playground](https://modeller.website/playground) — try System Design's modelling in the browser, no Initiative required.
- [Quick start](/docs/getting-started/quick-start) — install the tooling for System Design.

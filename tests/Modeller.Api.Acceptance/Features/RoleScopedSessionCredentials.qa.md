# QA procedure: Role-scoped session credentials (issue #146)

Plain-language script to confirm a respondent-scoped link to an Initiative
session can no longer act as the Facilitator. No implementation detail — just
what to click, call, or paste, and what you should see.

## Setup

1. Start a new Initiative session (supply an original change request, a
   Facilitator name, and a Domain Expert name).
2. Note the two links the session gives you back: one for the Facilitator,
   one for the Domain Expert. Confirm they are different links, not the same
   link with a different query value tacked on.
3. Using the Facilitator link, propose a question, send it to the Domain
   Expert, and (using the Domain Expert link) submit a response to it. Using
   the Facilitator link, select an intervention and record a gate evaluation
   that includes at least one failing check. Leave the response unaccepted
   and the gate finding un-dismissed, so both are still available to act on
   during the checks below.

## Part 1 — the Domain Expert's link cannot act as the Facilitator

For each of the following actions, use the Domain Expert's link (or, if
testing directly against the API, the Domain Expert's credential) and confirm
the action is refused and the session's usual structured error is returned
(not a blank error page, not a generic server error):

4. Send the proposed question to the Domain Expert.
5. Reject the proposed question.
6. Accept the Domain Expert's submitted response.
7. Select an intervention for the session.
8. Withdraw the selected intervention.
9. Record a gate evaluation for the session.
10. Dismiss the gate's failing check.
11. Finalize the session.
12. Reopen the session.

For each: confirm the session's state did not change (re-fetch it and check
nothing moved) and that editing the browser URL, removing a query value, or
calling the underlying API endpoint directly all fail the same way — this
must be enforced by the server, not just hidden by the page.

## Part 2 — the Facilitator's link still works exactly as before

13. Repeat every action in steps 4–11 (plus proposing a new question) using
    the Facilitator's link. Confirm every one still succeeds, exactly as it
    did before this change.

## Part 3 — role separation the other way

14. Using the Facilitator's link, try to submit a response to the sent
    question (the action that is supposed to belong to the Domain Expert).
    Confirm it is refused with the session's structured error.

## Part 4 — fetching the session respects the credential, not a typed-in role

15. Fetch the session using the Domain Expert's link. Confirm you see the
    Domain Expert's limited view (only questions actually sent, only their
    own responses, no gate evaluations, no intervention curation) — unchanged
    from before this feature.
16. Fetch the session using the Facilitator's link. Confirm you see the full
    view.
17. Fetch the session using the Domain Expert's link, but edit the request so
    it claims to be the Facilitator (however that claim is expressed — a
    query value, a header, anything client-supplied). Confirm you still get
    the Domain Expert's limited view — the claim is ignored.

## Part 5 — bad, expired, and cross-session credentials

18. Try to finalize the session using a credential that is garbled (edit a
    few characters of it). Confirm it is refused with the session's
    structured error, not a crash or an unrelated error.
19. Try to finalize the session using a Facilitator credential you know has
    expired (or wait for one to expire, if that is practical to test).
    Confirm it is refused with the session's structured error.
20. Start a second, unrelated Initiative session. Try to finalize the first
    session using the second session's Facilitator link. Confirm it is
    refused with the session's structured error — a valid Facilitator
    credential for the wrong session must not work.

## Part 6 — the links the website generates

21. From the Facilitator's cockpit page, copy the "share with Domain Expert"
    link it offers. Confirm that link, on its own, only ever behaves as the
    Domain Expert (it cannot be used to perform a Facilitator-only action,
    per Part 1).
22. Confirm the Facilitator's own page link, if copied and given to someone
    else, only ever behaves as the Facilitator — not as the Domain Expert.

## Pass criteria

- Every action in Part 1 is refused, with the session's structured error
  shape, and leaves the session unchanged.
- Every action in Part 2 still succeeds.
- Part 3's cross-check is refused the same way.
- Part 4 shows the credential — not a client-supplied role name — decides
  which view comes back.
- Part 5's three bad-credential cases are all refused with the structured
  error, never a crash or a silent success.
- Part 6 confirms the two links the website hands out are not interchangeable.

Feature: Role-scoped session credentials protect facilitator-only actions

  Today an Initiative session's Facilitator and Domain Expert are told apart
  only by which link they were handed — both links point at the same session
  and the session itself never checks who is calling. Anyone holding either
  link, or the session's identifier alone, can perform any facilitator action:
  send or reject a question, accept a response, select or withdraw an
  intervention, record or dismiss a gate evaluation, or finalize or reopen the
  session — even from the Domain Expert's link, even by calling the API
  directly instead of using a page at all.

  Starting a session must instead hand out two links that each carry their own
  role-scoped credential: a Facilitator link and a Domain Expert link. Every
  action against the session must present one of these credentials, and the
  session must judge the caller's role from the credential presented — never
  from a role name the caller types into a request. A credential that is
  missing, garbled, expired, or simply the wrong role for the action must be
  turned away with the same structured error shape the session already uses
  for its other failures, not a generic error.

  This does not change what the Domain Expert's view of the session hides or
  shows — only who is allowed to act.

  Background:
    Given a Facilitator has started a new Initiative session with a Domain Expert
    And a question has been proposed, sent to the Domain Expert, and answered
    And an intervention has been selected for the session
    And a gate has been evaluated for the session with a failing check

  Scenario: Starting a session hands out two distinct role-scoped links
    Then the Facilitator link and the Domain Expert link both identify the same session
    But the Facilitator link's credential is not the Domain Expert link's credential

  Scenario Outline: The Domain Expert's link cannot perform a facilitator-only action
    When the Domain Expert's link is used to <action>
    Then the action is refused
    And the refusal uses the session's structured error response
    And the session is unchanged

    Examples:
      | action                                       |
      | send the proposed question to the Domain Expert |
      | reject the proposed question                 |
      | accept the Domain Expert's submitted response |
      | select an intervention for the session        |
      | withdraw the selected intervention             |
      | record a gate evaluation for the session       |
      | dismiss the gate's failing check                |
      | finalize the session                           |
      | reopen the session                             |

  Scenario Outline: The Facilitator's link continues to perform every action it could before
    When the Facilitator's link is used to <action>
    Then the action succeeds

    Examples:
      | action                                       |
      | propose a new question                        |
      | send the proposed question to the Domain Expert |
      | reject the proposed question                 |
      | accept the Domain Expert's submitted response |
      | select an intervention for the session        |
      | withdraw the selected intervention             |
      | record a gate evaluation for the session       |
      | dismiss the gate's failing check                |
      | finalize the session                           |
      | reopen the session                             |

  Scenario: The Facilitator's link cannot submit the Domain Expert's response
    When the Facilitator's link is used to submit a response to the sent question
    Then the action is refused
    And the refusal uses the session's structured error response

  Scenario: Fetching the session with the Domain Expert's link returns the Domain Expert's own view
    When the session is fetched using the Domain Expert's link
    Then the response is the Domain Expert's role-scoped view of the session

  Scenario: Fetching the session with the Facilitator's link returns the full view
    When the session is fetched using the Facilitator's link
    Then the response is the full view of the session

  Scenario: A role claimed in the request cannot override the credential's real role
    When the session is fetched using the Domain Expert's link while the request claims to be the Facilitator
    Then the response is still the Domain Expert's role-scoped view of the session

  Scenario: A garbled credential is refused, not silently ignored
    When a garbled credential is used to finalize the session
    Then the action is refused
    And the refusal uses the session's structured error response

  Scenario: An expired credential is refused
    When an expired Facilitator credential is used to finalize the session
    Then the action is refused
    And the refusal uses the session's structured error response

  Scenario: A credential minted for a different session is refused here
    Given a second Initiative session has been started, with its own Facilitator and Domain Expert links
    When the second session's Facilitator link is used to finalize the first session
    Then the action is refused
    And the refusal uses the session's structured error response

  Scenario: The links the website hands out each carry only their own role's credential
    When the website builds the sharable links for the session
    Then the Facilitator's sharable link carries the Facilitator's credential and no other
    And the Domain Expert's sharable link carries the Domain Expert's credential and no other

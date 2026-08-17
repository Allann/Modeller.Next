---
title: Product event contract
---

# Product event contract

Contract version 1 permits only explicit events. Every event has `site`, `environment`, `release`, `internal`, `contract_version`, and a pseudonymous `distinct_id`.

## Events

- Common: `site_page_viewed`, `meaningful_use_started`, `outbound_link_followed`.
- Docs: `docs_article_viewed`, `docs_search_used`, `docs_call_to_action_selected`.
- Studio: `playground_opened`, `example_loaded`, `first_edit_made`, `analysis_completed`, `projection_viewed`, `workspace_downloaded`, `share_link_copied`.
- Initiative: `initiative_created`, `initiative_viewed`, `question_proposed`, `question_sent`, `response_submitted`, `response_accepted`, `gate_evaluated`, `intervention_selected`, `initiative_finalized`, `initiative_reopened`, `initiative_phase_reached`.

## Prohibited data

Events must not contain model source, Initiative text, questions, responses, file paths, diagnostic text, element text, full URLs, query strings, raw Initiative identifiers, share links, names, email addresses, or technical account identities. Initiative correlation uses the first 24 hexadecimal characters of a SHA-256 digest of the Initiative identifier.

Analytics failure must not change a user action. Local development uses disabled capture when no project key is configured.

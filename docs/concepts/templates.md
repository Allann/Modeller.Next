---
title: Templates
description: How validated template packs realise Modeller generation plans.
---

# Templates

A [template pack](/docs/concepts/ubiquitous-language#template-pack) is a pinned,
validated set of templates that turns proposed artifacts into text. It does not
discover domain meaning, choose output ownership, or write files.

## Pack contract

A pack declares:

- a stable pack ID and version;
- the supported generation-contract version;
- a renderer ID and version;
- artifact IDs, logical paths, owners, template IDs, and semantic inputs;
- the content of each referenced template.

`TemplatePackLoader` normalises and validates this manifest before planning.
Unsafe paths, duplicate identities, missing templates, incompatible contracts,
and unsupported renderers are diagnostics, not best-effort fallbacks.

## Generation flow

1. [Configuration](/docs/reference/configuration) selects compatible inputs.
2. The validated pack contributes deterministic artifact descriptors.
3. The [generation planner](/docs/reference/generation-plans) produces proposed artifacts with ownership and provenance.
4. A bounded [renderer adapter](/docs/reference/template-rendering) renders those artifacts.
5. [Output application](/docs/reference/output-application) previews or applies owned changes.

The reference renderer uses Scriban behind `IRendererAdapter`. Templates receive
an immutable artifact rendering context; they cannot read arbitrary files,
mutate the model, or choose additional output paths.

See [template packs](/docs/reference/template-packs) for manifest validation and
[template rendering](/docs/reference/template-rendering) for limits,
diagnostics, and provenance.

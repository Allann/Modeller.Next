---
title: Create a definition
description: Create an RML context and grow it into a multi-file model.
---

# Create a definition

Modeller definitions use Readable Modelling Language (RML) and the
`.modeller` extension. Begin with one complete, independently valid file:

```text
rml 1.0

context Acme Bookings
  version 1.0.0
end

entity Booking
  field Booking date
    type date
  end
  field Notes
    type text
    optional
  end
end
```

Save it as `model/context.modeller`. Names are business-facing and may contain
spaces. References to another definition use its readable name in quotes.

As the model grows, split declarations into folders such as `entities`,
`enumerations`, `facts`, `rules`, and `behaviours`. Declare every file in the
workspace's `.modeller/config.json`; generation does not scan the disk for
undeclared inputs.

Use these references while authoring:

- [RML 1.0 schema and syntax](/docs/reference/readable-modelling-language)
- [Definition kinds](/docs/concepts/definitions)
- [Data types](/docs/concepts/data-types)
- [Child Care multi-file example](/docs/reference/reference-project)

Routine definition files contain no UUIDs. Stable identities live in
`.modeller/identities.json` and are owned by tooling. Keep that file in source
control, but do not use it as the authoring surface.

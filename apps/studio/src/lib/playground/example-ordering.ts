// The playground's first-load example — inlined from samples/ordering rather
// than read off disk, since the playground build never has (or trusts) a
// local filesystem to read from. Kept in sync with samples/ordering by hand;
// see docs/architecture/decisions/hosted-workspace-api.mdx for why the hosted
// API takes documents inline rather than a workspace reference.
import type { ConfigurationDto, WorkspaceDocumentDto } from './api-client';

export const EXAMPLE_ORDERING_DOCUMENTS: readonly WorkspaceDocumentDto[] = [
  {
    path: 'model/context.modeller',
    content: `rml 1.0
context Ordering
  version 1.0.0
end
`,
  },
  {
    path: 'model/entities/order.modeller',
    content: `rml 1.0
entity Order
  lifecycle Order lifecycle
    stage Draft
    stage Placed
  end
end
`,
  },
  {
    path: 'model/facts/payment-confirmation.modeller',
    content: `rml 1.0
fact Payment is confirmed
  type truth
  export
end
`,
  },
  {
    path: 'model/rules/determine-order-readiness.modeller',
    content: `rml 1.0
rule Determine order readiness
  input "Payment is confirmed"
  when all
    fact "Payment is confirmed"
  end
  conclusion Ready
  end
  finding "Payment is confirmed" true order.payment-confirmed
  finding "Payment is confirmed" missing order.payment-required
  export
end
`,
  },
  {
    path: 'model/behaviours/place-order.modeller',
    content: `rml 1.0
behaviour Place order
  for "Order"
  requires "Determine order readiness"
  outcome Order placed
  end
  outcome Order rejected
  end
  transition Order placement
    lifecycle "Order lifecycle"
    from "Draft"
    to "Placed"
    outcome "Order placed"
  end
end
`,
  },
];

export const EXAMPLE_ORDERING_CONFIGURATION: ConfigurationDto = {
  generationContractVersion: '1.0',
  logicalOutputRoot: 'generated',
  profile: 'ordering-csharp',
};

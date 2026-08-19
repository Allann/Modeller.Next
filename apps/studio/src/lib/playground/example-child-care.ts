import type { ConfigurationDto, WorkspaceDocumentDto } from './api-client';

// A small browser-safe copy of the canonical samples/child-care vertical slice.
// Keep the source text aligned with that sample when its accepted model changes.
export const EXAMPLE_CHILD_CARE_DOCUMENTS: readonly WorkspaceDocumentDto[] = [
  { path: 'model/context.modeller', content: `rml 1.0
context Child Care
  version 1.0.0
end
` },
  { path: 'model/entities/accs-determination-application.modeller', content: `rml 1.0
entity ACCS determination application
  lifecycle ACCS determination application lifecycle
    stage Draft
    stage Submitted
  end
end
` },
  { path: 'model/facts/accs-eligibility.modeller', content: `rml 1.0
fact Active enrolment exists
  type truth
  export
end
fact Supporting evidence is held
  type truth
  export
end
` },
  { path: 'model/rules/determine-accs-eligibility.modeller', content: `rml 1.0
rule Determine ACCS eligibility
  input "Active enrolment exists"
  input "Supporting evidence is held"
  when all
    fact "Active enrolment exists"
    fact "Supporting evidence is held"
  end
  conclusion Eligible
  end
  finding "Active enrolment exists" true accs.active-enrolment-confirmed
  finding "Supporting evidence is held" true accs.supporting-evidence-confirmed
  finding "Active enrolment exists" missing accs.active-enrolment-required
  finding "Supporting evidence is held" missing accs.supporting-evidence-required
  export
end
` },
  { path: 'model/behaviours/submit-accs-determination-application.modeller', content: `rml 1.0
behaviour Submit ACCS determination application
  for "ACCS determination application"
  requires "Determine ACCS eligibility"
  outcome Application submitted
  end
  outcome Application rejected
  end
  transition Submit application
    lifecycle "ACCS determination application lifecycle"
    from "Draft"
    to "Submitted"
    outcome "Application submitted"
  end
end
` },
];

export const EXAMPLE_CHILD_CARE_CONFIGURATION: ConfigurationDto = {
  generationContractVersion: '1.0',
  logicalOutputRoot: 'generated',
  profile: 'child-care-csharp',
};

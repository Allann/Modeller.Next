const DOCS_BASE_URL = 'https://modeller.wiki/docs';

export type DocsHelpTopic = {
  /** Page path under /docs, without a leading slash. */
  path: string;
  /** An explicit `[#id]` heading id on that page, so a renamed heading does not break the link. */
  anchor?: string;
  /** What the link opens, used as the tooltip and the accessible name. */
  label: string;
};

/**
 * The docs sections that the Discovery Session links to from its info icons. Every anchor is a
 * `## Heading [#id]` in docs/ at the repo root — keep those ids when you edit the pages.
 * tests/e2e/DocsHelpLinks.spec.ts fails if one is missing.
 */
export const DOCS_HELP = {
  startDiscoverySession: { path: 'getting-started/start-a-discovery-session', label: 'How a Discovery Session works' },
  domainExpertLink: { path: 'getting-started/start-a-discovery-session', anchor: 'domain-expert-link', label: 'Send the Domain Expert their link' },
  questionLoop: { path: 'getting-started/start-a-discovery-session', anchor: 'question-loop', label: 'How the question loop works' },
  discoveryGate: { path: 'getting-started/start-a-discovery-session', anchor: 'discovery-gate', label: 'What the Discovery Gate checks' },
  finalize: { path: 'getting-started/start-a-discovery-session', anchor: 'finalize', label: 'Finalize and reopen an Initiative' },
  discover: { path: 'concepts/initiatives', anchor: 'discover', label: 'What Discover records' },
  frame: { path: 'concepts/initiatives', anchor: 'frame', label: 'What Frame records' },
  shape: { path: 'concepts/initiatives', anchor: 'shape', label: 'Shape, interventions, and the Shape Gate' },
  roles: { path: 'concepts/initiatives', anchor: 'roles', label: 'The Initiative record and its roles' },
  systemDesignHandoff: { path: 'concepts/initiatives', anchor: 'system-design-handoff', label: 'Continue into System Design' },
  workedExample: { path: 'guides/building-variation-initiative', label: 'Worked example: delayed building variations' },
} satisfies Record<string, DocsHelpTopic>;

export type DocsHelpTopicName = keyof typeof DOCS_HELP;

export function docsHelpUrl(name: DocsHelpTopicName): string {
  const topic: DocsHelpTopic = DOCS_HELP[name];
  return `${DOCS_BASE_URL}/${topic.path}${topic.anchor ? `#${topic.anchor}` : ''}`;
}

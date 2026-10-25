import { DOCS_HELP, docsHelpUrl, type DocsHelpTopicName } from '@/lib/docsHelp';

/** A small info icon that opens the matching docs section in a new tab. */
export function DocsHelpLink({ topic }: { topic: DocsHelpTopicName }) {
  const { label } = DOCS_HELP[topic];
  return (
    <a
      className="docs-help-link"
      href={docsHelpUrl(topic)}
      target="_blank"
      rel="noreferrer"
      title={label}
      aria-label={`Help: ${label} (opens the docs in a new tab)`}
    >
      <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
        <circle cx="12" cy="12" r="9" />
        <path d="M12 11v5" />
        <path d="M12 7.5h.01" />
      </svg>
    </a>
  );
}

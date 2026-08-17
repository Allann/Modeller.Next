'use client';

import { useState } from 'react';

interface MarkdownSection {
  heading: string;
  items: string[];
}

function parseInitiativeMarkdown(markdown: string): MarkdownSection[] {
  const sections: MarkdownSection[] = [];
  for (const line of markdown.split(/\r?\n/)) {
    if (line.startsWith('## ')) {
      sections.push({ heading: line.slice(3), items: [] });
    } else if (line.startsWith('- ') && sections.length > 0) {
      sections.at(-1)!.items.push(line.slice(2));
    }
  }
  return sections;
}

export function InitiativeDocument({ initiativeId, markdown }: { initiativeId: string; markdown: string }) {
  const [copied, setCopied] = useState(false);
  const sections = parseInitiativeMarkdown(markdown);

  return (
    <>
      <article className="initiative-document" aria-label="Final Initiative document">
        {sections.map((section) => (
          <section key={section.heading}>
            <h3>{section.heading}</h3>
            <ul>
              {section.items.map((item, index) => <li key={index}>{item}</li>)}
            </ul>
          </section>
        ))}
      </article>
      <div className="inline-form">
        <button
          className="secondary-action"
          type="button"
          onClick={() => {
            void navigator.clipboard.writeText(markdown).then(() => {
              setCopied(true);
              setTimeout(() => setCopied(false), 2000);
            });
          }}
        >
          {copied ? 'Copied' : 'Copy markdown'}
        </button>
        <button
          className="secondary-action"
          type="button"
          onClick={() => {
            const blob = new Blob([markdown], { type: 'text/markdown;charset=utf-8' });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `initiative-${initiativeId}.md`;
            link.click();
            URL.revokeObjectURL(url);
          }}
        >
          Download document
        </button>
      </div>
    </>
  );
}

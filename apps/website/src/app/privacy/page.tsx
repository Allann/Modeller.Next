import Link from 'next/link';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Privacy — Modeller Playground',
  description: 'What the Modeller playground stores, sends, and never keeps.',
};

export default function PrivacyPage() {
  return (
    <main>
      <Link href="/" className="breadcrumb">
        ← Home
      </Link>
      <p className="eyebrow">Privacy</p>
      <h1>What the playground does with your model</h1>

      <section>
        <h2>No accounts, no server-side drafts</h2>
        <p>
          The playground (at <a href="https://studio.modeller.website">studio.modeller.website</a>) has no
          sign-in. Whatever you type stays in your browser&apos;s <code>sessionStorage</code> for that tab —
          it is never written to a database or file on our servers, and it disappears when you close the tab
          unless you share or download it yourself.
        </p>
      </section>

      <section>
        <h2>Analysis and diagnostics</h2>
        <p>
          Editing the model sends its current text to our hosted analysis API so it can be parsed, validated,
          and projected into diagrams. That service logs request metadata only — document and projection
          counts, outcome, elapsed time, and diagnostic codes — never the document text itself, and never an
          identity registry. Unhandled errors return a generic response with no source text, stack trace, or
          infrastructure detail.
        </p>
      </section>

      <section>
        <h2>Share links</h2>
        <p>
          A share link encodes your model directly into the link itself (after the <code>#</code>), compressed
          in your browser — it is never uploaded anywhere to create the link. That also means anyone you send
          the link to can read what&apos;s in it, the same as any other link or file you might share; treat it
          accordingly.
        </p>
      </section>

      <section>
        <h2>Downloaded workspaces</h2>
        <p>
          Downloading a workspace builds a zip file entirely in your browser and hands it to your browser&apos;s
          own download mechanism. Nothing about that file is retained by us afterward.
        </p>
      </section>

      <section>
        <h2>Analytics</h2>
        <p>
          We use Vercel Analytics and Speed Insights for anonymized traffic and performance metrics (page
          views, load times) — never model content, and never tied to an account, since there isn&apos;t one.
        </p>
      </section>

      <section>
        <h2>Questions</h2>
        <p>
          Open an issue at{' '}
          <a href="https://github.com/Allann/Modeller.Next/issues">github.com/Allann/Modeller.Next</a>.
        </p>
      </section>
    </main>
  );
}

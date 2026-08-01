import Link from 'next/link';
import { ArrowRight, Braces, FileCode2, Layers3, Sparkles } from 'lucide-react';

export default function HomePage() {
  return (
    <main className="modeller-home">
      <section className="hero-shell">
        <div className="hero-copy">
          <div className="eyebrow"><Sparkles size={15} /> Intent-first software design</div>
          <h1>Describe the system.<br /><span>Generate the structure.</span></h1>
          <p className="hero-lede">
            Modeller turns concise domain definitions into consistent, production-ready
            software—without making your architecture disappear inside a black box.
          </p>
          <div className="hero-actions">
            <Link className="primary-action" href="/docs/getting-started">
              Start modelling <ArrowRight size={17} />
            </Link>
            <Link className="secondary-action" href="/docs/concepts">Explore the concepts</Link>
          </div>
        </div>
        <div className="model-preview" aria-label="Example Modeller domain definition">
          <div className="preview-bar"><span className="preview-dot" /><span>order.entity</span></div>
          <pre><code><span className="syntax-keyword">entity</span> Order{`\n`}  attributes{`\n`}    id         guid        generated{`\n`}    reference  text        unique{`\n`}    status     OrderStatus{`\n`}    created    datetime    generated{`\n`}  end{`\n`}<span className="syntax-keyword">end</span></code></pre>
          <div className="preview-result"><FileCode2 size={16} /> Domain, persistence, API and SDK outputs</div>
        </div>
      </section>
      <section className="principles-grid" aria-label="Modeller principles">
        <article><Braces /><h2>Intent is the source</h2><p>Capture what the business means once, independently of frameworks and infrastructure.</p></article>
        <article><Layers3 /><h2>Architecture stays visible</h2><p>Templates encode conventions while generated and handwritten code remain clearly separated.</p></article>
        <article><Sparkles /><h2>AI assists, you decide</h2><p>AI helps propose and explain models through the same explicit interfaces available to people.</p></article>
      </section>
    </main>
  );
}

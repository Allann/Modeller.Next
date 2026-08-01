import Link from 'next/link';
import { ArrowRight, Braces, Layers3, Sparkles } from 'lucide-react';

export default function HomePage() {
  return (
    <main className="modeller-home">
      <section className="brand-hero-shell">
        <div className="brand-hero-image">
          <img
            className="theme-hero-light"
            src="/brand/modeller-hero-light.png"
            alt="Modeller — Describe the system. Generate the structure."
          />
          <img
            className="theme-hero-dark"
            src="/brand/modeller-hero-dark.png"
            alt="Modeller — Describe the system. Generate the structure."
          />
        </div>
        <div className="hero-intro">
          <p>
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
      </section>
      <section className="principles-grid" aria-label="Modeller principles">
        <article><Braces /><h2>Intent is the source</h2><p>Capture what the business means once, independently of frameworks and infrastructure.</p></article>
        <article><Layers3 /><h2>Architecture stays visible</h2><p>Templates encode conventions while generated and handwritten code remain clearly separated.</p></article>
        <article><Sparkles /><h2>AI assists, you decide</h2><p>AI helps propose and explain models through the same explicit interfaces available to people.</p></article>
      </section>
    </main>
  );
}

import Link from 'next/link';
import Image from 'next/image';
import {
  ArrowRight,
  Bot,
  Braces,
  Check,
  Code2,
  Download,
  Layers3,
  RefreshCw,
  Route,
  ShieldCheck,
} from 'lucide-react';

const capabilities = [
  {
    icon: Braces,
    title: 'Model the meaning',
    text: 'Describe domains, concepts, relationships, behaviours, and constraints in a form people and tools can understand.',
  },
  {
    icon: Layers3,
    title: 'Choose the architecture',
    text: 'Apply an explicit template pack that turns intent into the conventions and boundaries your team has chosen.',
  },
  {
    icon: RefreshCw,
    title: 'Evolve without drift',
    text: 'Regenerate predictable structure as the model changes while keeping owned code clearly separated and safe.',
  },
];

const outcomes = [
  'A shared language for product and engineering',
  'Architecture expressed as reusable decisions',
  'Consistent projects without copy-and-paste scaffolding',
  'AI assistance grounded in an explicit model',
];

export default function HomePage() {
  return (
    <main className="modeller-home">
      <section className="marketing-hero">
        <div className="marketing-brand-hero">
          <Image
            className="theme-hero-light"
            src="/brand/modeller-hero-light.png"
            width={1731}
            height={909}
            priority
            alt="Modeller — Describe the system. Generate the structure."
          />
          <Image
            className="theme-hero-dark"
            src="/brand/modeller-hero-dark.png"
            width={1734}
            height={907}
            priority
            alt="Modeller — Describe the system. Generate the structure."
          />
        </div>
        <div className="marketing-hero-intro">
          <p>
            Modeller turns a clear domain model into consistent, production-ready
            software—so your team can spend less time rebuilding foundations and more
            time delivering what makes the product matter.
          </p>
          <div>
            <div className="hero-actions">
              <Link className="primary-action" href="/docs/getting-started">
                Start modelling <ArrowRight size={17} />
              </Link>
              <a className="secondary-action" href="https://modeller.website">
                Try the playground
              </a>
              <Link className="secondary-action" href="/docs/concepts">
                Explore the concepts
              </Link>
            </div>
            <p className="hero-note">Open foundations. Explicit architecture. Your code stays yours.</p>
          </div>
        </div>
      </section>

      <section className="promise-strip" aria-label="The Modeller promise">
        <div><strong>One model</strong><span>A durable source of intent</span></div>
        <div><strong>Visible decisions</strong><span>No architectural black box</span></div>
        <div><strong>Repeatable output</strong><span>Consistency across every project</span></div>
      </section>

      <section className="download-section">
        <div className="download-icon"><Download size={30} /></div>
        <div>
          <p className="eyebrow">Get Modeller <span className="coming-soon-badge">Coming soon</span></p>
          <h2>A desktop app for the whole workflow.</h2>
          <p>
            Model, generate, and review projects without leaving your machine. The
            Modeller desktop app for macOS, Windows, and Linux is on its way.
          </p>
        </div>
        <span className="primary-action download-action" aria-disabled="true">
          Download <span className="coming-soon-badge">Coming soon</span>
        </span>
      </section>

      <section className="marketing-section" id="why-modeller">
        <div className="section-heading">
          <p className="eyebrow"><Route size={15} /> From intention to implementation</p>
          <h2>Keep the meaning at the centre of the software.</h2>
          <p>
            Frameworks change. Infrastructure changes. The language of the business is
            what gives the system continuity. Modeller makes that language useful all
            the way into the codebase.
          </p>
        </div>
        <div className="capability-grid">
          {capabilities.map(({ icon: Icon, title, text }) => (
            <article key={title}>
              <span className="capability-icon"><Icon size={22} /></span>
              <h3>{title}</h3>
              <p>{text}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="workflow-section" id="how-it-works">
        <div className="workflow-copy">
          <p className="eyebrow"><Code2 size={15} /> How it works</p>
          <h2>Design once. Realise it deliberately.</h2>
          <p>
            Modeller connects the vocabulary of your domain to the structure of your
            application without hiding the decisions in between.
          </p>
          <ol className="workflow-steps">
            <li><span>1</span><div><strong>Describe</strong><p>Capture concepts, behaviours, rules, and relationships.</p></div></li>
            <li><span>2</span><div><strong>Shape</strong><p>Select the architecture and conventions that fit the system.</p></div></li>
            <li><span>3</span><div><strong>Generate</strong><p>Create navigable software structure with clear ownership boundaries.</p></div></li>
            <li><span>4</span><div><strong>Evolve</strong><p>Change the model and bring the implementation forward with it.</p></div></li>
          </ol>
        </div>
        <div className="outcome-panel">
          <p className="panel-kicker">What teams gain</p>
          <h3>Alignment that survives the handoff.</h3>
          <ul>
            {outcomes.map((outcome) => <li key={outcome}><Check size={17} />{outcome}</li>)}
          </ul>
        </div>
      </section>

      <section className="ai-section">
        <div className="ai-icon"><Bot size={30} /></div>
        <div>
          <p className="eyebrow">AI with something real to work from</p>
          <h2>Assistance grounded in your architecture.</h2>
          <p>
            AI can help discover, propose, explain, and evolve a model—but the model
            remains explicit, reviewable, and independent of any single provider.
            You keep the decisions; AI accelerates the conversation around them.
          </p>
        </div>
        <ShieldCheck className="ai-shield" size={72} aria-hidden="true" />
      </section>

      <section className="final-cta">
        <p className="eyebrow">Build from meaning</p>
        <h2>Give your next system a source of truth.</h2>
        <p>Explore the foundations of Modeller and follow the product as it takes shape.</p>
        <div className="hero-actions">
          <Link className="primary-action" href="/docs/getting-started">Read the getting started guide <ArrowRight size={17} /></Link>
          <Link className="secondary-action" href="/docs">Browse all documentation</Link>
        </div>
      </section>

      <footer className="marketing-footer">
        <div><span className="wordmark-mark">M</span><strong>Modeller</strong></div>
        <p>Intent-first software design.</p>
        <nav aria-label="Footer navigation"><Link href="/docs">Documentation</Link><Link href="/docs/concepts">Concepts</Link><a href="https://github.com/Allann/Modeller.Next">GitHub</a></nav>
      </footer>
    </main>
  );
}

import Link from 'next/link';
import {
  ArrowRight,
  Blocks,
  Bot,
  Braces,
  Check,
  CircleDot,
  Code2,
  Compass,
  Download,
  GitBranch,
  Layers3,
  Network,
  RefreshCw,
  ScanSearch,
  ShieldCheck,
} from 'lucide-react';

const problems = [
  {
    index: '01',
    title: 'The request is not the intent',
    text: 'Separate what someone asked to build from the business reality they want to change.',
  },
  {
    index: '02',
    title: 'Software is one intervention',
    text: 'Let process, people, policy, structure, information, and technology compete honestly.',
  },
  {
    index: '03',
    title: 'The reason must survive',
    text: 'Keep every chosen intervention connected to outcomes, evidence, assumptions, and decisions.',
  },
];

const journey = [
  {
    index: '01',
    label: 'Discover',
    tag: 'Business discovery',
    title: 'Understand the situation',
    text: 'Facilitate the conversation. Capture affected people, current pain, desired outcomes, constraints, assumptions, risks, and open questions.',
  },
  {
    index: '02',
    label: 'Frame',
    tag: 'Problem brief',
    title: 'Agree what better means',
    text: 'Turn the conversation into durable intent with explicit success measures, non-goals, and evidence.',
  },
  {
    index: '03',
    label: 'Shape',
    tag: 'Initiative shaping',
    title: 'Compare possible interventions',
    text: 'Explore process, people, organisation, policy, information, technology, experiments, and no action.',
  },
  {
    index: '04',
    label: 'Design',
    tag: 'Design workspaces',
    title: 'Describe the chosen change',
    text: 'Open the right design step for each selected intervention. A technology intervention continues into System Design, where the team describes actors, capabilities, behaviours, rules, workflows, and lifecycles.',
  },
];

const startSteps = [
  {
    index: '01',
    title: 'Capture the request',
    text: 'Open modeller.website, paste the change request in the words it arrived in, and name yourself and your Domain Expert.',
  },
  {
    index: '02',
    title: 'Invite the Domain Expert',
    text: 'Starting the Initiative gives you a shareable link. They see the question waiting for them, not the whole cockpit.',
  },
  {
    index: '03',
    title: 'Work through Discover and Frame',
    text: 'Ask, answer, and accept. Each accepted response becomes part of the structured record behind the Initiative.',
  },
];

const interventionTypes = [
  { title: 'Process', text: 'Remove friction or change how work flows.' },
  { title: 'People', text: 'Change roles, authority, capacity, or skills.' },
  { title: 'Policy', text: 'Change the rules that shape the outcome.' },
  { title: 'Technology', text: 'Model a system only when technology earns its place.' },
];

const exampleFlow = [
  { title: 'Remove a duplicate approval', kind: 'Process' },
  { title: 'Delegate low-risk decisions', kind: 'People' },
  { title: 'Automate document checks', kind: 'Technology' },
];

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
    <div className="modeller-home">
      <section className="marketing-hero">
        <div className="marketing-hero-intro">
          <p className="eyebrow"><Compass size={15} /> Start with what needs to change</p>
          <h1>&ldquo;Build us a new system.&rdquo;</h1>
          <p className="hero-correction">That is a proposed answer. It is not the problem.</p>
          <p>
            Modeller helps teams understand the business situation, decide what should
            change, and model the right intervention. Sometimes that intervention is
            software. Sometimes it is not.
          </p>
          <div className="hero-actions">
            <a className="primary-action" href="https://modeller.website">
              Start a Discovery Session <ArrowRight size={17} />
            </a>
            <a className="secondary-action" href="#journey">
              See the whole journey
            </a>
          </div>
          <p className="hero-note">
            Runs in your browser at <strong>modeller.website</strong>. No install, no account.
          </p>
        </div>
        <div className="trace-window" aria-label="From request to deliberate change">
          <div className="trace-window-bar">
            <span /><span /><span />
            <span className="trace-window-title">initiative / customer-approvals</span>
          </div>
          <div className="trace-graph">
            <div className="trace-node trace-node--request">
              <span className="trace-node-kicker">Request</span>
              <strong>Build a new approval system</strong>
            </div>
            <ArrowRight className="trace-arrow trace-arrow--one" size={18} aria-hidden="true" />
            <div className="trace-node trace-node--problem">
              <span className="trace-node-kicker">Observed problem</span>
              <strong>Decisions take 12 days</strong>
            </div>
            <ArrowRight className="trace-arrow trace-arrow--two" size={18} aria-hidden="true" />
            <div className="trace-node trace-node--outcome">
              <span className="trace-node-kicker">Desired outcome</span>
              <strong>Decide within 48 hours</strong>
            </div>
            <div className="trace-node trace-node--options">
              <span className="trace-node-kicker">Possible interventions</span>
              <span className="trace-pill">Process</span>
              <span className="trace-pill">Authority</span>
              <span className="trace-pill">Technology</span>
            </div>
            <ArrowRight className="trace-arrow trace-arrow--three" size={18} aria-hidden="true" />
            <div className="trace-node trace-node--design">
              <span className="trace-node-kicker">Selected technology intervention</span>
              <strong>System Design: document checks</strong>
            </div>
          </div>
          <p className="trace-status"><CircleDot size={14} aria-hidden="true" /> Meaning remains connected from problem and outcome through intervention to System Design</p>
        </div>
      </section>

      <section className="promise-strip" aria-label="The Modeller promise">
        <div><strong>Understand first</strong><span>Turn a requested fix into an evidence-backed problem</span></div>
        <div><strong>Choose deliberately</strong><span>Compare interventions against the outcome</span></div>
        <div><strong>Model what matters</strong><span>Carry the reason into every detailed model</span></div>
      </section>

      <section className="marketing-section" id="why-modeller">
        <div className="section-heading">
          <p className="eyebrow"><ScanSearch size={15} /> Before the architecture</p>
          <h2>A well-modelled system can still solve the wrong problem.</h2>
          <p>
            Most change requests arrive with the solution already embedded: automate this,
            replace that, build a portal. Modeller creates space to understand the situation
            before the requested implementation becomes inevitable.
          </p>
        </div>
        <div className="capability-grid">
          {problems.map(({ index, title, text }) => (
            <article key={title}>
              <span className="capability-icon capability-index">{index}</span>
              <h3>{title}</h3>
              <p>{text}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="marketing-section" id="journey">
        <div className="section-heading">
          <p className="eyebrow"><GitBranch size={15} /> One initiative, distinct ways of thinking</p>
          <h2>From business reality to deliberate change.</h2>
          <p>
            Each stage has its own purpose and interaction style. Modeller connects them
            without collapsing discovery, shaping, and detailed modelling into one
            oversized canvas.
          </p>
        </div>
        <ol className="journey-rows">
          {journey.map(({ index, label, tag, title, text }) => (
            <li key={label}>
              <span className="journey-index">{index}</span>
              <div>
                <p className="journey-label">{label}</p>
                <h3>{title}</h3>
                <p>{text}</p>
              </div>
              <em>{tag}</em>
            </li>
          ))}
        </ol>
      </section>

      <section className="marketing-section" id="start-discovery">
        <div className="section-heading">
          <p className="eyebrow"><Compass size={15} /> Begin at Discover</p>
          <h2>Start a Discovery Session in your browser.</h2>
          <p>
            Discovery runs at <strong>modeller.website</strong>. There is nothing to install
            and no account to create. You need a change request and someone who understands
            the situation it came from.
          </p>
        </div>
        <div className="capability-grid">
          {startSteps.map(({ index, title, text }) => (
            <article key={title}>
              <span className="capability-icon capability-index">{index}</span>
              <h3>{title}</h3>
              <p>{text}</p>
            </article>
          ))}
        </div>
        <div className="hero-actions">
          <a className="primary-action" href="https://modeller.website">
            Start a Discovery Session <ArrowRight size={17} />
          </a>
          <Link className="secondary-action" href="/docs/getting-started/start-a-discovery-session">
            Read the walkthrough first
          </Link>
        </div>
      </section>

      <section className="marketing-section" id="intervention">
        <div className="section-heading">
          <p className="eyebrow"><Blocks size={15} /> More than an IT solution</p>
          <h2>The right response may cross several kinds of change.</h2>
        </div>
        <div className="intervention-grid">
          {interventionTypes.map(({ title, text }) => (
            <article key={title}>
              <h3>{title}</h3>
              <p>{text}</p>
            </article>
          ))}
        </div>
        <div className="intervention-example">
          <small>Example initiative</small>
          <h3>Reduce customer approval time from twelve days to two.</h3>
          <ul>
            {exampleFlow.map(({ title, kind }) => (
              <li key={title}><span>{kind}</span>{title}</li>
            ))}
          </ul>
          <p>One outcome. Three coordinated interventions. Only the technology intervention continues into System Design.</p>
        </div>
      </section>

      <section className="marketing-section" id="system-design">
        <div className="section-heading">
          <p className="eyebrow"><Network size={15} /> The technology design step</p>
          <h2>System Design gives a technology intervention durable meaning.</h2>
          <p>
            System Design is one workspace within Modeller. Teams open it when a selected
            intervention requires technology, and describe the intended system through
            actors, capabilities, behaviours, workflows, rules, decisions, and lifecycles.
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

      <section className="workflow-section">
        <div className="workflow-copy">
          <p className="eyebrow"><Code2 size={15} /> How System Design works</p>
          <h2>Design once. Realise it deliberately.</h2>
          <p>
            System Design connects the vocabulary of your domain to the structure of your
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

      <section className="download-section">
        <div className="download-icon"><Download size={30} /></div>
        <div>
          <p className="eyebrow">Get System Design <span className="coming-soon-badge">Coming soon</span></p>
          <h2>A desktop app for the System Design workflow.</h2>
          <p>
            Model, generate, and review projects without leaving your machine. The
            System Design desktop app for macOS, Windows, and Linux is on its way.
          </p>
        </div>
        <span className="primary-action download-action" aria-disabled="true">
          Download <span className="coming-soon-badge">Coming soon</span>
        </span>
      </section>

      <section className="ai-section">
        <div className="ai-icon"><Bot size={30} /></div>
        <div>
          <p className="eyebrow">AI grounded in the initiative</p>
          <h2>Help the team think without making the decision for them.</h2>
          <p>
            AI can propose questions, expose gaps, compare interventions, and explain
            traceability. People remain responsible for accepting the problem statement,
            choosing the response, and owning the resulting design.
          </p>
        </div>
        <ShieldCheck className="ai-shield" size={72} aria-hidden="true" />
      </section>

      <section className="final-cta">
        <p className="eyebrow">Model the change</p>
        <h2>Understand first.<br />Change deliberately.</h2>
        <p>
          Follow Modeller as it grows from intent-first system design into a connected
          way to understand and shape organisational change.
        </p>
        <div className="hero-actions">
          <a className="primary-action" href="https://modeller.website">Start a Discovery Session <ArrowRight size={17} /></a>
          <Link className="secondary-action" href="/docs/getting-started">Install System Design tooling</Link>
          <a className="secondary-action" href="https://github.com/Allann/Modeller.Next/issues/82">Join the discussion</a>
        </div>
      </section>

      <footer className="marketing-footer">
        <div><span className="wordmark-mark">M</span><strong>Modeller</strong></div>
        <p>Understand first. Change deliberately.</p>
        <nav aria-label="Footer navigation"><Link href="/docs">Documentation</Link><Link href="/docs/concepts">Concepts</Link><a href="https://github.com/Allann/Modeller.Next">GitHub</a></nav>
      </footer>
    </div>
  );
}

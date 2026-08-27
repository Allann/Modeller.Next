import Link from 'next/link';
import type { ComponentType, ReactNode } from 'react';
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

type Icon = ComponentType<{ size?: number }>;

type GridItem = { badge?: string | Icon; title: string; text: string };
type RowItem = { badge: string; label?: string; title: string; text: string; tag?: string };
type StripItem = { title: string; text: string };

function GridBadge({ badge }: { badge: string | Icon }) {
  if (typeof badge === 'string') return badge;
  const BadgeIcon = badge;
  return <BadgeIcon size={20} />;
}

/** Boxed card grid. Badge is a number or an icon; omit it for plain cards. */
function Grid({ items, columns = 3 }: { items: GridItem[]; columns?: number }) {
  return (
    <div className="block-grid" style={{ '--grid-cols': columns } as React.CSSProperties}>
      {items.map(({ badge, title, text }) => (
        <article key={title}>
          {badge && (
            <span className="block-badge block-badge--md">
              <GridBadge badge={badge} />
            </span>
          )}
          <h3>{title}</h3>
          <p>{text}</p>
        </article>
      ))}
    </div>
  );
}

/** Sequenced row list. "divided" adds dividers plus optional label/tag; "compact" is a plain flex list. */
function RowList({ items, variant = 'divided' }: { items: RowItem[]; variant?: 'divided' | 'compact' }) {
  return (
    <ol className={`block-rows block-rows--${variant}`}>
      {items.map(({ badge, label, title, text, tag }) => (
        <li key={title}>
          <span className="block-badge block-badge--sm">{badge}</span>
          <div>
            {label && <p className="block-kicker">{label}</p>}
            <h3>{title}</h3>
            <p>{text}</p>
          </div>
          {tag && <em>{tag}</em>}
        </li>
      ))}
    </ol>
  );
}

/** Flush divider band, no card boxes. */
function Strip({ items }: { items: StripItem[] }) {
  return (
    <div className="block-strip" aria-label="The Modeller promise">
      {items.map(({ title, text }) => (
        <div key={title}>
          <strong>{title}</strong>
          <span>{text}</span>
        </div>
      ))}
    </div>
  );
}

/** Icon-flanked banner: badge, heading block, trailing content. */
function IconBanner({
  icon: BannerIcon,
  eyebrow,
  title,
  text,
  trailing,
  className,
}: {
  icon: Icon;
  eyebrow: ReactNode;
  title: string;
  text: string;
  trailing?: ReactNode;
  className: string;
}) {
  return (
    <section className={`block-banner ${className}`}>
      <span className="block-badge block-badge--lg"><BannerIcon size={30} /></span>
      <div>
        <p className="eyebrow">{eyebrow}</p>
        <h2>{title}</h2>
        <p>{text}</p>
      </div>
      {trailing}
    </section>
  );
}

const promises: StripItem[] = [
  { title: 'Understand first', text: 'Turn a requested fix into an evidence-backed problem' },
  { title: 'Choose deliberately', text: 'Compare interventions against the outcome' },
  { title: 'Model what matters', text: 'Carry the reason into every detailed model' }
];

const problems: GridItem[] = [
  { badge: '01', title: 'The request is not the intent', text: 'Separate what someone asked to build from the business reality they want to change.' },
  { badge: '02', title: 'Software is one intervention', text: 'Let process, people, policy, structure, information, and technology compete honestly.' },
  { badge: '03', title: 'The reason must survive', text: 'Keep every chosen intervention connected to outcomes, evidence, assumptions, and decisions.' }
];

const journey: RowItem[] = [
  {
    badge: '01',
    label: 'Discover',
    tag: 'Business discovery',
    title: 'Understand the situation',
    text: 'Facilitate the conversation. Capture affected people, current pain, desired outcomes, constraints, assumptions, risks, and open questions.',
  },
  {
    badge: '02',
    label: 'Frame',
    tag: 'Problem brief',
    title: 'Agree what better means',
    text: 'Turn the conversation into durable intent with explicit success measures, non-goals, and evidence.',
  },
  {
    badge: '03',
    label: 'Shape',
    tag: 'Initiative shaping',
    title: 'Compare possible interventions',
    text: 'Explore process, people, organisation, policy, information, technology, experiments, and no action.',
  },
  {
    badge: '04',
    label: 'Design',
    tag: 'Design workspaces',
    title: 'Describe the chosen change',
    text: 'Open the right design step for each selected intervention. A technology intervention continues into System Design, where the team describes actors, capabilities, behaviours, rules, workflows, and lifecycles.',
  },
];

const startSteps: GridItem[] = [
  {
    badge: '01',
    title: 'Capture the request',
    text: 'Open modeller.website, paste the change request in the words it arrived in, and name yourself and your Domain Expert.',
  },
  {
    badge: '02',
    title: 'Invite the Domain Expert',
    text: 'Starting the Initiative gives you a shareable link. They see the question waiting for them, not the whole cockpit.',
  },
  {
    badge: '03',
    title: 'Work through Discover and Frame',
    text: 'Ask, answer, and accept. Each accepted response becomes part of the structured record behind the Initiative.',
  },
];

const interventionTypes: GridItem[] = [
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

const capabilities: GridItem[] = [
  {
    badge: Braces,
    title: 'Model the meaning',
    text: 'Describe domains, concepts, relationships, behaviours, and constraints in a form people and tools can understand.',
  },
  {
    badge: Layers3,
    title: 'Choose the architecture',
    text: 'Apply an explicit template pack that turns intent into the conventions and boundaries your team has chosen.',
  },
  {
    badge: RefreshCw,
    title: 'Evolve without drift',
    text: 'Regenerate predictable structure as the model changes while keeping owned code clearly separated and safe.',
  },
];

const workflowSteps: RowItem[] = [
  { badge: '1', title: 'Describe', text: 'Capture concepts, behaviours, rules, and relationships.' },
  { badge: '2', title: 'Shape', text: 'Select the architecture and conventions that fit the system.' },
  { badge: '3', title: 'Generate', text: 'Create navigable software structure with clear ownership boundaries.' },
  { badge: '4', title: 'Evolve', text: 'Change the model and bring the implementation forward with it.' },
];

const outcomes = [
  'A shared language for product and engineering',
  'Architecture expressed as reusable decisions',
  'Consistent projects without copy-and-paste scaffolding',
  'AI assistance grounded in an explicit model',
];

function withWorkspaceDownload(url: string): string {
  const separator = url.includes('?') ? '&' : '?';
  return `${url}${separator}download=workspace`;
}

const windowsStudioInstallerUrl =
  'https://github.com/Allann/Modeller.Next/releases/download/studio-windows-latest/ModellerStudioSetup.exe';

export default function HomePage() {
  const playgroundUrl = process.env.NEXT_PUBLIC_PLAYGROUND_URL ?? 'https://modeller.website/playground?example=child-care';
  const playgroundDownloadUrl = withWorkspaceDownload(playgroundUrl);
  return (
    <div className="modeller-home">
      <section className="marketing-hero">
        <div className="marketing-hero-intro">
          <p className="eyebrow"><Compass size={15} /> Start with what needs to change</p>
          <h1>&ldquo;Build us a new system.&rdquo;</h1>
          <p className="hero-correction">That is a proposed answer. It is not the problem.</p>
          <p>
            Modeller helps teams understand the business situation, decide what should change, and model the right intervention. Sometimes that intervention is software. Sometimes it is not.
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

      <Strip items={promises} />

      <section className="marketing-section" id="why-modeller">
        <div className="section-heading">
          <p className="eyebrow"><ScanSearch size={15} /> Before the architecture</p>
          <h2>A well-modelled system can still solve the wrong problem.</h2>
          <p>
            Most change requests arrive with the solution already embedded: automate this, replace that, build a portal. Modeller creates space to understand the situation before the requested implementation becomes inevitable.
          </p>
        </div>
        <Grid items={problems} columns={3} />
      </section>

      <section className="marketing-section" id="journey">
        <div className="section-heading">
          <p className="eyebrow"><GitBranch size={15} /> One initiative, distinct ways of thinking</p>
          <h2>From business reality to deliberate change.</h2>
          <p>
            Each stage has its own purpose and interaction style. Modeller connects them without collapsing discovery, shaping, and detailed modelling into one oversized canvas.
          </p>
        </div>
        <RowList items={journey} variant="divided" />
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
        <Grid items={startSteps} columns={3} />
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
        <Grid items={interventionTypes} columns={4} />
        <div className="accent-panel">
          <div className="accent-panel-kicker">Example initiative</div>
          <h3>Reduce customer approval time from twelve days to two.</h3>
          <ul className="pill-list">
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
        <Grid items={capabilities} columns={3} />
      </section>

      <section className="marketing-section" id="case-studies">
        <div className="section-heading">
          <p className="eyebrow"><Blocks size={15} /> Case studies</p>
          <h2>See one change travel from a user need to a working model.</h2>
          <p>
            Real examples show where Modeller helps today and where the product still needs work.
            More case studies can join this collection as the public workflows grow.
          </p>
        </div>
        <div className="case-study-grid">
          <article>
            <p className="block-kicker">Child Care</p>
            <h3>Request more evidence before an ACCS eligibility decision</h3>
            <p>Follow a case worker need through the lifecycle, rule, validation, diagram, and safe generation workflow.</p>
            <div className="hero-actions">
              <Link className="primary-action" href="/docs/case-studies/child-care-request-more-information">Read the case study <ArrowRight size={17} /></Link>
              <a className="secondary-action" href={playgroundUrl}>Open the Child Care model</a>
            </div>
          </article>
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
          <RowList items={workflowSteps} variant="compact" />
        </div>
        <div className="accent-panel">
          <p className="accent-panel-kicker">What teams gain</p>
          <h3>Alignment that survives the handoff.</h3>
          <ul className="check-list">
            {outcomes.map((outcome) => <li key={outcome}><Check size={17} />{outcome}</li>)}
          </ul>
        </div>
      </section>

      <IconBanner
        className="download-banner"
        icon={Download}
        eyebrow="Take the model local"
        title="Install Studio, then open the workspace."
        text="Install Modeller Studio for Windows, download the Child Care workspace, then double-click the package. Studio opens it locally with the bundled Modeller tools."
        trailing={
          <div className="download-actions">
            <a className="primary-action download-action" href={windowsStudioInstallerUrl}>
              Install Studio for Windows <ArrowRight size={17} />
            </a>
            <a className="secondary-action download-action" href={playgroundDownloadUrl}>
              Download workspace
            </a>
          </div>
        }
      />

      <IconBanner
        className="ai-banner"
        icon={Bot}
        eyebrow="AI grounded in the initiative"
        title="Help the team think without making the decision for them."
        text="AI can propose questions, expose gaps, compare interventions, and explain traceability. People remain responsible for accepting the problem statement, choosing the response, and owning the resulting design."
        trailing={<ShieldCheck className="ai-shield" size={56} aria-hidden="true" />}
      />

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

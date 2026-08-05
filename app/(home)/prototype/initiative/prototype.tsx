'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useEffect } from 'react';
import {
  ArrowLeft,
  ArrowRight,
  Blocks,
  Check,
  ChevronLeft,
  ChevronRight,
  CircleDot,
  Compass,
  GitBranch,
  Lightbulb,
  Network,
  ScanSearch,
  Sparkles,
  Users,
} from 'lucide-react';
import styles from './prototype.module.css';

const variants = [
  { key: 'A', name: 'The whole journey' },
  { key: 'B', name: 'The decision canvas' },
  { key: 'C', name: 'The provocative story' },
  { key: 'D', name: 'Challenge, then journey' },
] as const;

type VariantKey = (typeof variants)[number]['key'];

const interventionTypes = [
  ['Process', 'Remove friction or change how work flows.'],
  ['People', 'Change roles, authority, capacity, or skills.'],
  ['Policy', 'Change the rules that shape the outcome.'],
  ['Technology', 'Model a system only when technology earns its place.'],
];

function PrototypeNotice() {
  return <div className={styles.notice}>Concept prototype · positioning exploration · links are illustrative</div>;
}

function Brand() {
  return <div className={styles.brand}><span>M</span><strong>Modeller</strong></div>;
}

function JourneyDiagram() {
  return (
    <div className={styles.journeyDiagram} aria-label="The Modeller journey">
      {[
        ['01', 'Discover', 'Understand the situation'],
        ['02', 'Frame', 'Agree what better means'],
        ['03', 'Shape', 'Compare interventions'],
        ['04', 'Model', 'Design the chosen change'],
      ].map(([number, title, copy], index) => (
        <div className={styles.journeyStep} key={title}>
          <span>{number}</span><div><strong>{title}</strong><small>{copy}</small></div>
          {index < 3 && <ArrowRight aria-hidden="true" />}
        </div>
      ))}
    </div>
  );
}

function VariantA() {
  return (
    <main className={`${styles.page} ${styles.variantA}`}>
      <PrototypeNotice />
      <header className={styles.header}><Brand /><nav><a href="#journey">How it works</a><a href="#interventions">What you can model</a><span>Explore Modeller <ArrowRight /></span></nav></header>
      <section className={styles.heroA}>
        <div className={styles.heroCopy}>
          <p className={styles.eyebrow}><Compass /> From uncertainty to intentional change</p>
          <h1>Model the change.<br/><em>Not just the software.</em></h1>
          <p className={styles.lede}>Start with the business situation. Discover what actually needs to change. Compare the options. Then model the intervention that makes sense—whether it is a process, a team, a policy, or a system.</p>
          <div className={styles.actions}><button>Explore the journey <ArrowRight /></button><a href="#journey">See an example</a></div>
        </div>
        <div className={styles.orbitGraphic} aria-label="An initiative connecting problems to interventions">
          <div className={styles.orbitCore}><Sparkles /><strong>Initiative</strong><small>Make customer approvals faster</small></div>
          <span className={styles.orbitOne}>Problem</span><span className={styles.orbitTwo}>Outcome</span><span className={styles.orbitThree}>Interventions</span><span className={styles.orbitFour}>Evidence</span>
        </div>
      </section>
      <section className={styles.proofStrip}><span>One initiative</span><span>Many possible interventions</span><span>Every decision traceable to why</span></section>
      <section className={styles.journeySection} id="journey">
        <p className={styles.eyebrow}><GitBranch /> One connected journey</p>
        <h2>Do not begin with a solution.<br/>Earn your way to one.</h2>
        <p>Modeller keeps discovery, intervention choices, and detailed models connected without pretending they are the same activity.</p>
        <JourneyDiagram />
      </section>
      <section className={styles.interventions} id="interventions">
        <div><p className={styles.eyebrow}><Blocks /> Beyond the IT project</p><h2>The answer may not be an application.</h2><p>Shape several plausible responses before committing money and attention to one.</p></div>
        <div className={styles.interventionGrid}>{interventionTypes.map(([title, copy]) => <article key={title}><CircleDot /><h3>{title}</h3><p>{copy}</p></article>)}</div>
      </section>
      <footer className={styles.cta}><p>Bring the reason all the way into the model.</p><h2>Understand first.<br/>Change deliberately.</h2><button>Follow Modeller’s evolution <ArrowRight /></button></footer>
    </main>
  );
}

function VariantB() {
  const options = [
    { name: 'Remove second approval', type: 'Process', score: 'Strong fit' },
    { name: 'Delegate decision authority', type: 'People', score: 'Explore' },
    { name: 'Automate document checks', type: 'Technology', score: 'Explore' },
  ];
  return (
    <main className={`${styles.page} ${styles.variantB}`}>
      <PrototypeNotice />
      <header className={styles.header}><Brand /><nav><a href="#example">See it work</a><a href="#principles">Principles</a><span>Join the preview <ArrowRight /></span></nav></header>
      <section className={styles.heroB}>
        <div className={styles.heroCopy}>
          <p className={styles.eyebrow}><ScanSearch /> A workspace for deciding what should change</p>
          <h1>Before you model a system, model the decision.</h1>
          <p className={styles.lede}>Turn an incoming request into an understood problem, measurable outcomes, and a deliberate portfolio of interventions.</p>
          <div className={styles.actions}><button>See the workspace <ArrowRight /></button><a href="#principles">Why this matters</a></div>
        </div>
        <div className={styles.canvas} id="example">
          <div className={styles.canvasTop}><span>Initiative / Faster approvals</span><span className={styles.liveDot}>Framing</span></div>
          <div className={styles.canvasProblem}><small>Observed situation</small><strong>Customer applications take 12 days to approve</strong><p>Customers abandon time-sensitive applications and staff spend hours chasing status.</p></div>
          <div className={styles.canvasOutcome}><small>Outcome</small><strong>Eligible applications decided in 48 hours</strong><span>Measure agreed</span></div>
          <div className={styles.optionList}><small>Interventions being shaped</small>{options.map(option => <div key={option.name}><CircleDot/><p><strong>{option.name}</strong><span>{option.type}</span></p><em>{option.score}</em></div>)}</div>
          <div className={styles.trace}><Network/><span>Every option remains linked to the problem, outcome, evidence, and assumptions.</span></div>
        </div>
      </section>
      <section className={styles.valueBand} id="principles">
        <article><Lightbulb/><strong>Discover together</strong><p>A facilitated conversation turns “just build this” into durable business intent.</p></article>
        <article><GitBranch/><strong>Compare honestly</strong><p>Process, people, policy, and technology compete on the same outcomes.</p></article>
        <article><Network/><strong>Keep the why</strong><p>Chosen changes stay traceable as teams move into detailed modelling.</p></article>
      </section>
      <section className={styles.splitStory}><div><p className={styles.eyebrow}>One product, distinct modes</p><h2>Different thinking deserves different spaces.</h2></div><p>Discovery remains conversational. Shaping makes alternatives visible. Detailed modelling becomes precise. Modeller connects the work without flattening it into one giant canvas.</p></section>
      <footer className={styles.cta}><h2>Make better changes,<br/>not merely better systems.</h2><button>Explore the proposal <ArrowRight /></button></footer>
    </main>
  );
}

function VariantC() {
  return (
    <main className={`${styles.page} ${styles.variantC}`}>
      <PrototypeNotice />
      <header className={styles.header}><Brand /><nav><a href="#manifesto">Our point of view</a><a href="#path">The path</a><span>Read the proposal <ArrowRight /></span></nav></header>
      <section className={styles.heroC}>
        <p className={styles.kicker}>Most failed solutions started as unquestioned answers.</p>
        <h1>“Build us<br/>a new system.”</h1>
        <div className={styles.strike}><span>That is not a problem statement.</span></div>
        <p className={styles.lede}>Modeller helps teams uncover what is really happening, decide what should change, and only then model the right response.</p>
        <div className={styles.actions}><button>Start with the problem <ArrowRight /></button></div>
      </section>
      <section className={styles.manifesto} id="manifesto">
        <p>Sometimes the right answer is software.</p><p>Sometimes it is a simpler process.</p><p>Sometimes people need authority, training, or time.</p><p>Sometimes the policy is the problem.</p><strong>Modeller should help you tell the difference.</strong>
      </section>
      <section className={styles.pathC} id="path">
        <div><span>01</span><ScanSearch/><h2>Understand</h2><p>Replace the requested fix with an evidence-backed account of the situation and the outcome that matters.</p></div>
        <div><span>02</span><GitBranch/><h2>Choose</h2><p>Shape competing interventions. Surface assumptions. Record why one path earned commitment.</p></div>
        <div><span>03</span><Blocks/><h2>Model</h2><p>Give the chosen change durable meaning—from processes and responsibilities to behaviours, rules, and systems.</p></div>
      </section>
      <section className={styles.quote}><blockquote>“What are we trying to change—and why do we believe this will change it?”</blockquote><p>The question your model should always be able to answer.</p></section>
      <footer className={styles.cta}><Brand/><h2>From business reality<br/>to deliberate change.</h2><button>Follow the idea <ArrowRight /></button></footer>
    </main>
  );
}

function VariantD() {
  return (
    <main className={`${styles.page} ${styles.variantD}`}>
      <PrototypeNotice />
      <header className={styles.header}>
        <Brand />
        <nav>
          <a href="#why">Why Modeller</a>
          <a href="#journey-d">The journey</a>
          <a href="#system-d">System Design</a>
          <span>Explore the proposal <ArrowRight /></span>
        </nav>
      </header>

      <section className={styles.heroD}>
        <div className={styles.heroDCopy}>
          <p className={styles.eyebrow}><Compass /> Start with what needs to change</p>
          <h1>“Build us a new system.”</h1>
          <p className={styles.correction}>That is a proposed answer. It is not the problem.</p>
          <p className={styles.lede}>Modeller helps teams understand the business situation, decide what should change, and model the right intervention. Sometimes that intervention is software. Sometimes it is not.</p>
          <div className={styles.actions}>
            <button>See the whole journey <ArrowRight /></button>
            <a href="#why">Why this matters</a>
          </div>
          <p className={styles.heroNoteD}>Durable intent. Visible decisions. Deliberate change.</p>
        </div>
        <div className={styles.modelWindowD} aria-label="From request to deliberate change">
          <div className={styles.windowBarD}><span /><span /><span /><small>initiative / customer-approvals</small></div>
          <div className={styles.modelGraphD}>
            <div className={styles.requestNodeD}><small>Request</small><strong>Build a new approval system</strong></div>
            <ArrowRight className={styles.graphArrowOneD} />
            <div className={styles.problemNodeD}><small>Observed problem</small><strong>Decisions take 12 days</strong></div>
            <ArrowRight className={styles.graphArrowTwoD} />
            <div className={styles.outcomeNodeD}><small>Desired outcome</small><strong>Decide within 48 hours</strong></div>
            <div className={styles.optionNodeD}><small>Possible interventions</small><span>Process</span><span>Authority</span><span>Technology</span></div>
          </div>
          <div className={styles.windowStatusD}><Check /> Meaning remains connected from problem to intervention</div>
        </div>
      </section>

      <section className={styles.promiseD} aria-label="The expanded Modeller promise">
        <div><strong>Understand first</strong><span>Turn requested fixes into an evidence-backed problem</span></div>
        <div><strong>Choose deliberately</strong><span>Compare interventions against the outcome</span></div>
        <div><strong>Model what matters</strong><span>Carry the reason into every detailed model</span></div>
      </section>

      <section className={styles.whyD} id="why">
        <div className={styles.sectionIntroD}>
          <p className={styles.eyebrow}><ScanSearch /> Before the architecture</p>
          <h2>A well-modelled system can still solve the wrong problem.</h2>
          <p>Most change requests arrive with the solution already embedded: automate this, replace that, build a portal. Modeller should create enough space to understand the situation before the requested implementation becomes inevitable.</p>
        </div>
        <div className={styles.problemCardsD}>
          <article><span>01</span><h3>The request is not the intent</h3><p>Separate what someone asked to build from the business reality they want to change.</p></article>
          <article><span>02</span><h3>Software is one intervention</h3><p>Let process, people, policy, structure, information, and technology compete honestly.</p></article>
          <article><span>03</span><h3>The “why” must survive</h3><p>Keep every chosen intervention connected to outcomes, evidence, assumptions, and decisions.</p></article>
        </div>
      </section>

      <section className={styles.journeyD} id="journey-d">
        <div className={styles.sectionIntroD}>
          <p className={styles.eyebrow}><GitBranch /> One initiative, distinct ways of thinking</p>
          <h2>From business reality to deliberate change.</h2>
          <p>Each stage has its own purpose and interaction style. Modeller connects them without collapsing discovery, shaping, and detailed modelling into one oversized canvas.</p>
        </div>
        <div className={styles.journeyRowsD}>
          <article><span>01</span><div><small>Discover</small><h3>Understand the situation</h3><p>Facilitate the conversation. Capture affected people, current pain, desired outcomes, constraints, assumptions, risks, and open questions.</p></div><em>Business discovery</em></article>
          <article><span>02</span><div><small>Frame</small><h3>Agree what better means</h3><p>Turn the conversation into durable intent with explicit success measures, non-goals, and evidence.</p></div><em>Problem brief</em></article>
          <article><span>03</span><div><small>Shape</small><h3>Compare possible interventions</h3><p>Explore process, people, organisation, policy, information, technology, experiments, and no action.</p></div><em>Initiative shaping</em></article>
          <article><span>04</span><div><small>Design</small><h3>Describe the chosen change</h3><p>Open the right design step for each selected intervention. Technology interventions continue into System Design, where the team describes actors, capabilities, behaviours, rules, workflows, and lifecycles.</p></div><em>Design workspaces</em></article>
        </div>
      </section>

      <section className={styles.interventionD}>
        <div className={styles.sectionIntroD}>
          <p className={styles.eyebrow}><Blocks /> More than an IT solution</p>
          <h2>The right response may cross several kinds of change.</h2>
        </div>
        <div className={styles.interventionGridD}>
          {interventionTypes.map(([title, copy], index) => <article key={title}><span>0{index + 1}</span><CircleDot /><h3>{title}</h3><p>{copy}</p></article>)}
        </div>
        <div className={styles.exampleD}>
          <div><small>Example initiative</small><h3>Reduce customer approval time from twelve days to two.</h3></div>
          <div className={styles.exampleFlowD}><span>Remove duplicate approval<em>Process</em></span><span>Delegate low-risk decisions<em>People</em></span><span>Automate document checks<em>Technology</em></span></div>
          <p>One outcome. Three coordinated interventions. Only the technology intervention continues into System Design.</p>
        </div>
      </section>

      <section className={styles.systemD} id="system-d">
        <div className={styles.systemArtworkD}>
          <div className={styles.systemPreviewBarD}><span>System Design</span><small>customer-approvals / application-decision</small></div>
          <div className={styles.systemPreviewCanvasD}>
            <div className={styles.capabilityNodeD}><small>Capability</small><strong>Application decisioning</strong></div>
            <div className={styles.actorNodeD}><small>Actor</small><strong>Assessor</strong></div>
            <div className={styles.behaviourNodeD}><small>Behaviour</small><strong>Decide application</strong></div>
            <div className={styles.ruleNodeD}><small>Rule</small><strong>Low-risk eligibility</strong></div>
            <span className={styles.previewLineOneD} /><span className={styles.previewLineTwoD} /><span className={styles.previewLineThreeD} />
          </div>
          <div className={styles.systemPreviewStatusD}><CircleDot /> Linked to technology intervention: automate document checks</div>
        </div>
        <div className={styles.systemCopyD}>
          <p className={styles.eyebrow}><Network /> The technology design step</p>
          <h2>System Design gives an IT intervention durable meaning.</h2>
          <p>System Design is one workspace within Modeller. Teams use it when a selected intervention requires technology. It describes the intended system through actors, capabilities, behaviours, workflows, rules, decisions, lifecycles, and architecture.</p>
          <ul><li><Check /> Start System Design only when technology is part of the chosen response.</li><li><Check /> Connect every system capability to the intervention and outcome it supports.</li><li><Check /> Keep business discovery, initiative shaping, and technical design distinct but traceable.</li></ul>
        </div>
      </section>

      <section className={styles.aiD}>
        <div><Sparkles /></div>
        <section><p className={styles.eyebrow}>AI grounded in the initiative</p><h2>Help the team think without making the decision for them.</h2><p>AI can propose questions, expose gaps, compare interventions, and explain traceability. People accept the problem statement, choose the response, and own the resulting model.</p></section>
      </section>

      <footer className={styles.ctaD}>
        <p className={styles.eyebrow}>Model the change</p>
        <h2>Understand first.<br/>Change deliberately.</h2>
        <p>Follow Modeller as it grows from intent-first software design into a connected way to understand and shape organisational change.</p>
        <div className={styles.actions}><button>Explore the proposal <ArrowRight /></button><a href="https://github.com/Allann/Modeller.Next/issues/81">Join the discussion</a></div>
      </footer>
    </main>
  );
}

function PrototypeSwitcher({ current }: { current: VariantKey }) {
  const router = useRouter();
  const index = variants.findIndex(item => item.key === current);
  const select = (offset: number) => router.replace(`?variant=${variants[(index + offset + variants.length) % variants.length].key}`, { scroll: false });

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement;
      if (target.matches('input, textarea, [contenteditable="true"]')) return;
      if (event.key === 'ArrowLeft') select(-1);
      if (event.key === 'ArrowRight') select(1);
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  });

  if (process.env.NODE_ENV === 'production') return null;
  return <div className={styles.switcher}><button onClick={() => select(-1)} aria-label="Previous variant"><ChevronLeft/></button><span><b>{current}</b> · {variants[index].name}</span><button onClick={() => select(1)} aria-label="Next variant"><ChevronRight/></button></div>;
}

export function InitiativeLandingPrototype() {
  const requested = useSearchParams().get('variant')?.toUpperCase();
  const current: VariantKey = variants.some(item => item.key === requested) ? requested as VariantKey : 'A';
  return <><Link href="/" className={styles.back}><ArrowLeft/> Current landing page</Link>{current === 'A' && <VariantA/>}{current === 'B' && <VariantB/>}{current === 'C' && <VariantC/>}{current === 'D' && <VariantD/>}<PrototypeSwitcher current={current}/></>;
}

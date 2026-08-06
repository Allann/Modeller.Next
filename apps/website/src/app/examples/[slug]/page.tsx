import Link from 'next/link';
import { notFound } from 'next/navigation';
import type { Metadata } from 'next';
import { examples, getExample, getExampleData, type ProjectionView } from '@/lib/examples';
import { GraphDiagram } from '@/components/GraphDiagram';

export function generateStaticParams() {
  return examples.map((example) => ({ slug: example.slug }));
}

export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> {
  const { slug } = await params;
  const example = getExample(slug);
  if (!example) return {};
  return {
    title: `${example.title} — Modeller Playground`,
    description: example.description,
    openGraph: { title: example.title, description: example.description },
  };
}

function ProjectionSection({ title, view }: { title: string; view: ProjectionView }) {
  if (!view.supported) {
    return (
      <section>
        <h2>{title}</h2>
        <p>This projection kind is not implemented yet — it&apos;s honestly omitted rather than shown empty.</p>
      </section>
    );
  }
  if (!view.graph || view.graph.nodes.length === 0) {
    return (
      <section>
        <h2>{title}</h2>
        <p>No {title.toLowerCase()} is declared for this example yet.</p>
      </section>
    );
  }
  return (
    <section>
      <h2>{title}</h2>
      <div className="diagram">
        <GraphDiagram graph={view.graph} />
      </div>
    </section>
  );
}

export default async function ExamplePage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const example = getExample(slug);
  if (!example) notFound();
  const data = getExampleData(example.slug);

  return (
    <main>
      <Link href="/examples" className="breadcrumb">
        ← All examples
      </Link>
      <p className="eyebrow">{example.kind === 'intro' ? 'Start here' : 'Deep dive'}</p>
      <h1>{example.title}</h1>
      <p>{example.description}</p>

      <ProjectionSection title="Lifecycle" view={data.views.Lifecycle} />
      <ProjectionSection title="Rule & decision" view={data.views.RuleDecision} />

      <section>
        <h2>RML source</h2>
        {data.source.map((document) => (
          <div key={document.path} className="source-doc">
            <span className="eyebrow">{document.path}</span>
            <pre>{document.content}</pre>
          </div>
        ))}
      </section>
    </main>
  );
}

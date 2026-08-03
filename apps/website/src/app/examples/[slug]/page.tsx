import { notFound } from 'next/navigation';
import type { Metadata } from 'next';
import { examples, getExample, getExampleData, type ProjectionView } from '@/lib/examples';

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
        <p>This projection kind is not implemented yet.</p>
      </section>
    );
  }
  if (!view.graph) {
    return (
      <section>
        <h2>{title}</h2>
        <p>No {title.toLowerCase()} projection is declared for this example yet.</p>
      </section>
    );
  }
  return (
    <section>
      <h2>{title}</h2>
      <ul className="graph-list">
        {view.graph.nodes.map((node) => (
          <li key={node.id}>{node.label}</li>
        ))}
      </ul>
      {view.graph.edges.length > 0 && (
        <ul className="graph-list">
          {view.graph.edges.map((edge) => {
            const source = view.graph!.nodes.find((node) => node.id === edge.sourceId);
            const target = view.graph!.nodes.find((node) => node.id === edge.targetId);
            return (
              <li key={edge.id}>
                {source?.label ?? edge.sourceId} → {target?.label ?? edge.targetId}
                {edge.label ? ` (${edge.label})` : ''}
              </li>
            );
          })}
        </ul>
      )}
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
      <p className="eyebrow">{example.kind === 'intro' ? 'Start here' : 'Deep dive'}</p>
      <h1>{example.title}</h1>
      <p>{example.description}</p>

      <ProjectionSection title="Lifecycle" view={data.views.Lifecycle} />
      <ProjectionSection title="Rule &amp; decision" view={data.views.RuleDecision} />

      <section>
        <h2>RML source</h2>
        {data.source.map((document) => (
          <div key={document.path}>
            <p className="eyebrow">{document.path}</p>
            <pre>{document.content}</pre>
          </div>
        ))}
      </section>
    </main>
  );
}

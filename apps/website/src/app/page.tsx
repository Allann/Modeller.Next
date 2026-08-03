import Link from 'next/link';
import { examples } from '@/lib/examples';

export default function HomePage() {
  return (
    <main>
      <p className="eyebrow">Explore Modeller</p>
      <h1>Explore a real Modeller example</h1>
      <p>
        Open a populated model, read the plain-language source that declares it, and see how
        Modeller projects its lifecycle and decisions — no install required. Ready to run it
        locally? See <a href="https://modeller.wiki">modeller.wiki</a>.
      </p>
      {examples.map((example) => (
        <Link key={example.slug} href={`/examples/${example.slug}`} className="card">
          <p className="eyebrow">{example.kind === 'intro' ? 'Start here' : 'Deep dive'}</p>
          <h2>{example.title}</h2>
          <p>{example.tagline}</p>
        </Link>
      ))}
    </main>
  );
}

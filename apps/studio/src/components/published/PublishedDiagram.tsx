'use client';

import { useMemo, useState } from 'react';
import { GraphCanvas } from '@/components/workbench/GraphCanvas';
import type { PublishedSnapshot } from '@/lib/published-snapshot';

export function PublishedDiagram({ snapshot }: { snapshot: PublishedSnapshot }) {
  const [selected, setSelected] = useState(0);
  const diagrams = useMemo(() => snapshot.diagrams.filter((diagram) => diagram.graph), [snapshot.diagrams]);
  const diagram = diagrams[selected];

  if (!diagram?.graph) return <p>This published model has no diagrams.</p>;
  return (
    <main className="published-diagram">
      <header>
        <h1>Published Modeller diagram</h1>
        <label>
          Diagram
          <select value={selected} onChange={(event) => setSelected(Number(event.target.value))}>
            {diagrams.map((item, index) => <option key={`${item.view}-${item.root.id}`} value={index}>{item.view}: {item.root.name}</option>)}
          </select>
        </label>
      </header>
      <GraphCanvas graph={diagram.graph} />
    </main>
  );
}

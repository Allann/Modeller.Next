'use client';

import { useEffect, useState } from 'react';
import { editor as monacoEditor } from 'monaco-editor';

interface ProblemRow {
  key: string;
  path: string;
  line: number;
  message: string;
  severity: number;
}

// Reads Monaco's own marker system, which monaco-editor-wrapper's language
// client already populates from LSP publishDiagnostics — no second
// diagnostics pipeline.
export function ProblemsPanel({ onNavigate }: { onNavigate: (path: string, line: number) => void }) {
  const [problems, setProblems] = useState<ProblemRow[]>([]);

  useEffect(() => {
    const collect = () => {
      const markers = monacoEditor.getModelMarkers({});
      setProblems(
        markers.map((marker) => ({
          key: `${marker.resource.toString()}:${marker.startLineNumber}:${marker.message}`,
          path: marker.resource.path.replace(/^\/workspace\//, ''),
          line: marker.startLineNumber,
          message: marker.message,
          severity: marker.severity,
        })),
      );
    };
    collect();
    const subscription = monacoEditor.onDidChangeMarkers(collect);
    return () => subscription.dispose();
  }, []);

  return (
    <div className="problems">
      {problems.length === 0 ? (
        <div className="problems-empty">No problems.</div>
      ) : (
        problems.map((problem) => (
          <div key={problem.key} className="problem-row" onClick={() => onNavigate(problem.path, problem.line)}>
            <span>{problem.severity >= 8 ? '⛔' : '⚠'}</span>
            <span>
              {problem.path}:{problem.line} — {problem.message}
            </span>
          </div>
        ))
      )}
    </div>
  );
}

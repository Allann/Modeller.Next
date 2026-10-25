'use client';

import { useState } from 'react';
import { CHECKS_BY_GATE, GATE_CHECK_LABELS, type GateCheckResultDto, type GateKind, type InitiativeSessionDto } from '@/lib/initiativeTypes';
import { initiativeApi } from '@/lib/initiativeApi';
import type { LocalAgent } from '@/lib/localAgent/useLocalAgent';
import type { RunAction } from './types';
import { DocsHelpLink } from '@/components/DocsHelpLink';

export function GateSection({ kind, session, credential, run, aiAvailable, agentApiKey, localAgent }: { kind: GateKind; session: InitiativeSessionDto; credential: string; run: RunAction; aiAvailable: boolean; agentApiKey: string; localAgent: LocalAgent | null }) {
  const evaluation = kind === 'Discovery' ? session.latestDiscoveryGateEvaluation : session.latestShapeGateEvaluation;
  const [manualResults, setManualResults] = useState<Record<string, { passed: boolean; reason: string }>>({});
  const [evaluating, setEvaluating] = useState(false);

  const checks = CHECKS_BY_GATE[kind];

  function evaluateWithAi() {
    setEvaluating(true);
    void run(async () => {
      if (!localAgent) return initiativeApi.recordGateEvaluation(session.id, credential, kind, null, agentApiKey);
      // The local model's results go through the manual-results path, with its recommended question.
      const evaluation = await localAgent.evaluateGate(session, kind);
      return initiativeApi.recordGateEvaluation(session.id, credential, kind, evaluation.results, undefined, evaluation.recommendedQuestion);
    }).finally(() => setEvaluating(false));
  }

  function submitManual() {
    const results: GateCheckResultDto[] = checks.map((check) => ({
      check,
      passed: manualResults[check]?.passed ?? false,
      reason: manualResults[check]?.reason || 'Not stated.',
    }));
    void run(() => initiativeApi.recordGateEvaluation(session.id, credential, kind, results));
  }

  return (
    <section aria-label={`${kind} Gate`}>
      <h2>{kind} Gate <DocsHelpLink topic={kind === 'Discovery' ? 'discoveryGate' : 'shape'} /></h2>
      <p className="hero-note">Advisory only — never blocks proceeding.</p>
      {aiAvailable ? (
        <div className="inline-form">
          <button className="secondary-action" disabled={evaluating} onClick={evaluateWithAi}>
            {evaluating ? 'Asking AI…' : 'Ask AI to evaluate'}
          </button>
        </div>
      ) : null}

      {evaluation ? (
        <ul className="item-list">
          {evaluation.results.map((result) => (
            <li key={result.check}>
              <span className={result.passed ? 'badge badge-pass' : 'badge badge-fail'}>{result.passed ? 'Pass' : 'Flagged'}</span>{' '}
              {GATE_CHECK_LABELS[result.check]} — {result.reason}
              {!result.passed && (
                <button
                  className="link-action"
                  onClick={() => void run(() => initiativeApi.dismissGateFinding(session.id, credential, kind, result.check, 'Accepted for now.'))}
                >
                  Dismiss
                </button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <details>
          <summary>No AI configured? Evaluate manually</summary>
          <div className="manual-gate-form">
            {checks.map((check) => (
              <label key={check} className="manual-gate-row">
                <input
                  type="checkbox"
                  checked={manualResults[check]?.passed ?? false}
                  onChange={(event) =>
                    setManualResults((prev) => ({ ...prev, [check]: { ...prev[check], passed: event.target.checked, reason: prev[check]?.reason ?? '' } }))
                  }
                />
                <span>{GATE_CHECK_LABELS[check]}</span>
                <input
                  placeholder="reason"
                  value={manualResults[check]?.reason ?? ''}
                  onChange={(event) =>
                    setManualResults((prev) => ({ ...prev, [check]: { ...prev[check], passed: prev[check]?.passed ?? false, reason: event.target.value } }))
                  }
                />
              </label>
            ))}
            <button className="secondary-action" onClick={submitManual}>
              Record manual evaluation
            </button>
          </div>
        </details>
      )}
    </section>
  );
}

'use client';

import { useState } from 'react';
import { CHECKS_BY_GATE, type GateCheckResultDto, type GateKind, type InitiativeSessionDto } from '@/lib/initiativeTypes';
import { initiativeApi } from '@/lib/initiativeApi';
import type { RunAction } from './types';

export function GateSection({ kind, session, run }: { kind: GateKind; session: InitiativeSessionDto; run: RunAction }) {
  const evaluation = kind === 'Discovery' ? session.latestDiscoveryGateEvaluation : session.latestShapeGateEvaluation;
  const [manualResults, setManualResults] = useState<Record<string, { passed: boolean; reason: string }>>({});

  const checks = CHECKS_BY_GATE[kind];

  function submitManual() {
    const results: GateCheckResultDto[] = checks.map((check) => ({
      check,
      passed: manualResults[check]?.passed ?? false,
      reason: manualResults[check]?.reason || 'Not stated.',
    }));
    void run(() => initiativeApi.recordGateEvaluation(session.id, kind, results));
  }

  return (
    <section aria-label={`${kind} Gate`}>
      <h2>{kind} Gate</h2>
      <p className="hero-note">Advisory only — never blocks proceeding.</p>
      <div className="inline-form">
        <button
          className="secondary-action"
          onClick={() => void run(() => initiativeApi.recordGateEvaluation(session.id, kind, null))}
        >
          Ask AI to evaluate
        </button>
      </div>

      {evaluation ? (
        <ul className="item-list">
          {evaluation.results.map((result) => (
            <li key={result.check}>
              <span className={result.passed ? 'badge badge-pass' : 'badge badge-fail'}>{result.passed ? 'Pass' : 'Flagged'}</span>{' '}
              {result.check} — {result.reason}
              {!result.passed && (
                <button
                  className="link-action"
                  onClick={() => void run(() => initiativeApi.dismissGateFinding(session.id, kind, result.check, 'Accepted for now.'))}
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
                {check}
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

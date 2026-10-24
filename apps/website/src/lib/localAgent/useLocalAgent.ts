'use client';

import { useCallback, useEffect, useState } from 'react';
import type { GateKind, InitiativeField, InitiativeSessionDto } from '../initiativeTypes';
import { isModelCached, isWebGpuAvailable, loadLocalEngine, type LoadProgress, type LocalChatEngine } from './engine';
import { evaluateGateLocally } from './localGate';
import {
  parseInterventions,
  parseQuestion,
  proposeInterventionsPrompt,
  proposeQuestionPrompt,
  type GateEvaluation,
  type InterventionSuggestion,
} from './prompts';

/** The three Agent Participant tasks the cockpit offers, answered by the in-browser model. */
export type LocalAgent = {
  proposeQuestion(session: InitiativeSessionDto, field: InitiativeField): Promise<string>;
  proposeInterventions(session: InitiativeSessionDto): Promise<InterventionSuggestion[]>;
  evaluateGate(session: InitiativeSessionDto, kind: GateKind): Promise<GateEvaluation>;
};

function createLocalAgent(engine: LocalChatEngine): LocalAgent {
  return {
    proposeQuestion: async (session, field) => parseQuestion(await engine.complete(proposeQuestionPrompt(session, field))),
    proposeInterventions: async (session) => parseInterventions(await engine.complete(proposeInterventionsPrompt(session))),
    evaluateGate: (session, kind) => evaluateGateLocally(session, kind, (prompt) => engine.complete(prompt)),
  };
}

export type LocalAgentState =
  | { status: 'checking' }
  | { status: 'unsupported' }
  | { status: 'idle'; cached: boolean }
  | { status: 'loading'; progress: LoadProgress }
  | { status: 'ready'; agent: LocalAgent }
  | { status: 'error'; message: string; cached: boolean };

// Per-browser convenience only: remembers that this Facilitator opted in, so a later visit starts the
// already-downloaded model without asking again. Storage can be unavailable (private windows), so
// every access is guarded and the default is "not opted in".
const OPT_IN_KEY = 'modeller:local-model:enabled';

function readOptIn(): boolean {
  try {
    return window.localStorage.getItem(OPT_IN_KEY) === '1';
  } catch {
    return false;
  }
}

function writeOptIn(): void {
  try {
    window.localStorage.setItem(OPT_IN_KEY, '1');
  } catch {
    // Without storage the Facilitator simply opts in again next visit.
  }
}

export function useLocalAgent(): { state: LocalAgentState; start: () => void } {
  const [state, setState] = useState<LocalAgentState>({ status: 'checking' });

  const start = useCallback(() => {
    writeOptIn();
    setState({ status: 'loading', progress: { fraction: 0, text: 'Starting…' } });
    loadLocalEngine((progress) => setState({ status: 'loading', progress }))
      .then((engine) => setState({ status: 'ready', agent: createLocalAgent(engine) }))
      .catch((error: unknown) =>
        setState({ status: 'error', message: error instanceof Error ? error.message : String(error), cached: false }),
      );
  }, []);

  useEffect(() => {
    let cancelled = false;
    if (!isWebGpuAvailable()) {
      const timer = window.setTimeout(() => setState({ status: 'unsupported' }), 0);
      return () => window.clearTimeout(timer);
    }
    void isModelCached().then((cached) => {
      if (cancelled) return;
      if (cached && readOptIn()) start();
      else setState({ status: 'idle', cached });
    });
    return () => {
      cancelled = true;
    };
  }, [start]);

  return { state, start };
}

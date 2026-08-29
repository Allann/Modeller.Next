'use client';

import { useCallback, useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { API_BASE_URL, InitiativeApiError, initiativeApi } from './initiativeApi';
import type { InitiativeSessionDto } from './initiativeTypes';

const SESSION_UPDATED_EVENT = 'InitiativeSessionUpdated';
// A tab left open in the background has no visible listener for live updates, but SignalR's
// automatic reconnect would otherwise keep retrying it forever — every retry is a fresh request
// to Modeller.Api, and on a scale-to-zero host that means a full cold boot every cycle. Stopping
// the connection after a grace period of being hidden turns an abandoned tab into zero traffic.
const IDLE_DISCONNECT_MS = 60_000;
export type InitiativeConnectionStatus = 'connecting' | 'live' | 'reconnecting' | 'polling';

/** Loads an Initiative session and keeps it live via #90's SignalR hub — a bare "something
 * changed" notification, so this just refetches rather than trying to apply a partial delta
 * (matches InitiativeHub's own deliberately-thin design, src/Modeller.Api/Initiative/InitiativeHub.cs).
 * `credential` is the role-scoped session credential (issue #146) — the server derives the
 * projection (full vs. Domain Expert) from it, never from a role name this hook could claim. */
export function useInitiativeSession(id: string, credential: string) {
  const [session, setSession] = useState<InitiativeSessionDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [connectionStatus, setConnectionStatus] = useState<InitiativeConnectionStatus>('connecting');

  const refetch = useCallback(async () => {
    if (!credential) {
      setError('Missing session credential — use the link this Initiative gave you.');
      setLoading(false);
      return;
    }
    try {
      const latest = await initiativeApi.get(id, credential);
      setSession(latest);
      setError(null);
    } catch (err) {
      setError(err instanceof InitiativeApiError ? err.message : 'Could not load this Initiative.');
    } finally {
      setLoading(false);
    }
  }, [id, credential]);

  useEffect(() => {
    // react-hooks/set-state-in-effect flags this, but refetch only calls setState after its
    // internal await — this is the standard fetch-on-mount pattern, not a synchronous setState.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void refetch();

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/initiative`)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: ({ previousRetryCount }) => Math.min(1000 * 2 ** previousRetryCount, 30_000),
      })
      .build();
    let disposed = false;
    let restartTimer: ReturnType<typeof setTimeout> | undefined;
    let idleTimer: ReturnType<typeof setTimeout> | undefined;
    // Distinct from idleTimer: idleTimer only spans the grace period before we act, while this
    // stays true across the connection.stop() -> onclose round-trip so onclose knows not to
    // schedule its own restart on top of the deliberate idle disconnect.
    let idleDisconnected = false;

    async function joinAndRefresh() {
      await connection.invoke('JoinSession', id);
      if (disposed) return;
      setConnectionStatus('live');
      await refetch();
    }

    async function start() {
      if (disposed || connection.state !== signalR.HubConnectionState.Disconnected) return;
      setConnectionStatus('connecting');
      try {
        await connection.start();
        await joinAndRefresh();
      } catch {
        if (disposed) return;
        setConnectionStatus('polling');
        restartTimer = setTimeout(() => void start(), 5000);
      }
    }

    connection.on(SESSION_UPDATED_EVENT, () => {
      void refetch();
    });

    connection.onreconnecting(() => setConnectionStatus('reconnecting'));
    connection.onreconnected(() => {
      void joinAndRefresh().catch(() => setConnectionStatus('polling'));
    });
    connection.onclose(() => {
      if (disposed || idleDisconnected) return;
      setConnectionStatus('polling');
      restartTimer = setTimeout(() => void start(), 5000);
    });

    void start();

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'hidden') {
        idleTimer = setTimeout(() => {
          idleTimer = undefined;
          idleDisconnected = true;
          if (restartTimer) clearTimeout(restartTimer);
          void connection.stop();
        }, IDLE_DISCONNECT_MS);
        return;
      }
      if (idleTimer) {
        clearTimeout(idleTimer);
        idleTimer = undefined;
        return;
      }
      if (idleDisconnected) {
        idleDisconnected = false;
        void start();
      }
    };
    document.addEventListener('visibilitychange', handleVisibilityChange);

    return () => {
      disposed = true;
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      if (idleTimer) clearTimeout(idleTimer);
      if (restartTimer) clearTimeout(restartTimer);
      void connection.stop();
    };
  }, [id, refetch]);

  useEffect(() => {
    const refreshIfVisible = () => {
      if (document.visibilityState === 'visible') void refetch();
    };
    const interval = setInterval(
      refreshIfVisible,
      connectionStatus === 'live' ? 15_000 : 5000,
    );
    document.addEventListener('visibilitychange', refreshIfVisible);
    window.addEventListener('online', refreshIfVisible);
    return () => {
      clearInterval(interval);
      document.removeEventListener('visibilitychange', refreshIfVisible);
      window.removeEventListener('online', refreshIfVisible);
    };
  }, [connectionStatus, refetch]);

  return { session, error, loading, connectionStatus, refetch };
}

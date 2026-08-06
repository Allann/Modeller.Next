'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { API_BASE_URL, InitiativeApiError, initiativeApi } from './initiativeApi';
import type { InitiativeSessionDto } from './initiativeTypes';

const SESSION_UPDATED_EVENT = 'InitiativeSessionUpdated';

/** Loads an Initiative session and keeps it live via #90's SignalR hub — a bare "something
 * changed" notification, so this just refetches rather than trying to apply a partial delta
 * (matches InitiativeHub's own deliberately-thin design, src/Modeller.Api/Initiative/InitiativeHub.cs). */
export function useInitiativeSession(id: string, viewerRole?: 'DomainExpert') {
  const [session, setSession] = useState<InitiativeSessionDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const refetch = useCallback(async () => {
    try {
      const latest = await initiativeApi.get(id, viewerRole);
      setSession(latest);
      setError(null);
    } catch (err) {
      setError(err instanceof InitiativeApiError ? err.message : 'Could not load this Initiative.');
    } finally {
      setLoading(false);
    }
  }, [id, viewerRole]);

  useEffect(() => {
    // react-hooks/set-state-in-effect flags this, but refetch only calls setState after its
    // internal await — this is the standard fetch-on-mount pattern, not a synchronous setState.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void refetch();

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/initiative`)
      .withAutomaticReconnect()
      .build();
    connectionRef.current = connection;

    connection.on(SESSION_UPDATED_EVENT, () => {
      void refetch();
    });

    connection
      .start()
      .then(() => connection.invoke('JoinSession', id))
      .catch(() => {
        // Realtime is an enhancement, not a requirement — the page still works via the initial
        // fetch and any manual refresh; a failed hub connection is silently non-fatal.
      });

    return () => {
      void connection.stop();
    };
  }, [id, refetch]);

  return { session, error, loading, refetch };
}

import type { InitiativeConnectionStatus } from '@/lib/useInitiativeSession';

const LABELS: Record<InitiativeConnectionStatus, string> = {
  connecting: 'Connecting',
  live: 'Live',
  reconnecting: 'Reconnecting',
  polling: 'Polling',
};

export function ConnectionStatus({ status }: { status: InitiativeConnectionStatus }) {
  return (
    <span className={`connection-status connection-status-${status}`} role="status">
      <span className="connection-status-dot" aria-hidden="true" />
      {LABELS[status]}
    </span>
  );
}

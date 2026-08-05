'use client';

export type NoticeKind = 'info' | 'analyzing' | 'error';

export interface Notice {
  kind: NoticeKind;
  text: string;
}

// Shared by the live analyze status (idle/analyzing/error) and one-off share/export/decode
// notices — PlaygroundWorkbench decides which single notice takes priority at any given moment.
export function StatusBanner({ notice }: { notice: Notice | undefined }) {
  if (!notice) return null;

  return (
    <div className={`playground-status playground-status-${notice.kind}`} role="status">
      {notice.text}
    </div>
  );
}

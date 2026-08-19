'use client';

export type NoticeKind = 'info' | 'analyzing' | 'error';

export interface Notice {
  kind: NoticeKind;
  text: string;
}

// The analysis status is always present so analysis requests do not change the workbench height.
export function StatusBanner({ notice }: { notice: Notice }) {
  return (
    <div className={`playground-status playground-status-${notice.kind}`}>
      {notice.text}
    </div>
  );
}

'use client';

export type AnalysisStatus = 'idle' | 'analyzing' | 'error';

export function StatusBanner({ status, errorMessage }: { status: AnalysisStatus; errorMessage?: string }) {
  if (status === 'idle') return null;

  return (
    <div className={`playground-status playground-status-${status}`} role="status">
      {status === 'analyzing'
        ? 'Analyzing…'
        : `Couldn't reach the analysis service${errorMessage ? ` (${errorMessage})` : ''}. Your draft is unaffected — it will retry on your next edit.`}
    </div>
  );
}

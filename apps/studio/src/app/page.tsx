'use client';

import dynamic from 'next/dynamic';

// Monaco fundamentally can't run server-side (it touches `window` at module
// scope), so the whole Workbench — and everything it imports — is loaded
// client-only.
const WorkbenchShell = dynamic(() => import('@/components/workbench/WorkbenchShell').then((mod) => mod.WorkbenchShell), {
  ssr: false,
});

export default function Page() {
  return (
    <main>
      <WorkbenchShell />
    </main>
  );
}

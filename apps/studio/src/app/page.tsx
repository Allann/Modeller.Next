'use client';

import dynamic from 'next/dynamic';

// Monaco fundamentally can't run server-side (it touches `window` at module
// scope), so the whole Workbench — and everything it imports — is loaded
// client-only.
const WorkbenchShell = dynamic(() => import('@/components/workbench/WorkbenchShell').then((mod) => mod.WorkbenchShell), {
  ssr: false,
});

// The playground is a separate, deployment-time mode (NEXT_PUBLIC_MODELLER_STUDIO_MODE) rather
// than a route toggled at runtime — it has no local filesystem/subprocess access to fall back to,
// so a single build is either local Studio or the public playground, never both. See
// docs/architecture/decisions/hosted-workspace-api.mdx and issue #72.
const PlaygroundWorkbench = dynamic(
  () => import('@/components/playground/PlaygroundWorkbench').then((mod) => mod.PlaygroundWorkbench),
  { ssr: false },
);

const isPlayground = process.env.NEXT_PUBLIC_MODELLER_STUDIO_MODE === 'playground';

export default function Page() {
  return <main>{isPlayground ? <PlaygroundWorkbench /> : <WorkbenchShell />}</main>;
}

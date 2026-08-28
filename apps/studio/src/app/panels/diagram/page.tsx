import type { Metadata } from 'next';
import { DiagramPanelClient } from './DiagramPanelClient';

// A page-specific title, not the root layout's plain "Modeller Studio" — Electron syncs each
// BrowserWindow's title to whatever the loaded page's own title becomes (page-title-updated),
// so without this every detached window ends up titled identically and indistinguishably.
export const metadata: Metadata = { title: 'Diagram — Modeller Studio' };

export default function DiagramPanelPage() {
  return <DiagramPanelClient />;
}

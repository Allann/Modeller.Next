import type { Metadata } from 'next';
import { GenerationPanelClient } from './GenerationPanelClient';

// See panels/diagram/page.tsx's comment — Electron syncs the BrowserWindow title to the page title.
export const metadata: Metadata = { title: 'Generated Files — Modeller Studio' };

export default function GenerationPanelPage() {
  return <GenerationPanelClient />;
}

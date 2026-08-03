import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'Modeller Playground',
  description: 'Explore real Modeller examples in your browser — no install required.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>{children}</body>
    </html>
  );
}

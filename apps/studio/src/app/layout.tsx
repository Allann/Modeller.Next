import type { Metadata } from 'next';
import { Analytics } from '@vercel/analytics/next';
import { ProductAnalytics } from '@/components/ProductAnalytics';
import './globals.css';

export const metadata: Metadata = {
  title: 'Modeller Studio',
  description: 'Local definition design tool for Modeller.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>
        {children}
        <Analytics />
        <ProductAnalytics />
      </body>
    </html>
  );
}

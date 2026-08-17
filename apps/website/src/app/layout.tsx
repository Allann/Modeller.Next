import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import { ThemeProvider } from '@/components/ThemeProvider';
import { SiteHeader } from '@/components/SiteHeader';
import { SiteFooter } from '@/components/SiteFooter';
import { Analytics } from '@vercel/analytics/next';
import { ProductAnalytics } from '@/components/ProductAnalytics';
import './globals.css';

const inter = Inter({ subsets: ['latin'] });

export const metadata: Metadata = {
  title: { default: 'Modeller: start an Initiative', template: '%s' },
  description: 'Capture a change request and work through Discover, Frame, and Shape before deciding whether a technology intervention is even the right response.',
  metadataBase: new URL('https://modeller.website'),
  openGraph: {
    siteName: 'Modeller',
    type: 'website',
  },
  twitter: {
    card: 'summary_large_image',
  },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={inter.className} suppressHydrationWarning>
      <body>
        <ThemeProvider>
          <SiteHeader />
          {children}
          <SiteFooter />
        </ThemeProvider>
        <Analytics />
        <ProductAnalytics />
      </body>
    </html>
  );
}

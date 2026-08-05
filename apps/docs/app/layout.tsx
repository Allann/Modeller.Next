import { RootProvider } from 'fumadocs-ui/provider/next';
import './global.css';
import { Inter } from 'next/font/google';
import type { Metadata } from 'next';
import { SpeedInsights } from '@vercel/speed-insights/next';
import { Analytics } from '@vercel/analytics/next';

const inter = Inter({
  subsets: ['latin'],
});

export const metadata: Metadata = {
  metadataBase: new URL(
    process.env.VERCEL_PROJECT_PRODUCTION_URL
      ? `https://${process.env.VERCEL_PROJECT_PRODUCTION_URL}`
      : 'http://localhost:3000',
  ),
  title: {
    default: 'Modeller: understand first, change deliberately.',
    template: '%s | Modeller',
  },
  description:
    'Modeller helps teams understand a business situation, choose the right intervention, and, when technology is part of the answer, describe it through System Design.',
  openGraph: {
    title: 'Modeller: understand first, change deliberately.',
    description:
      'Modeller helps teams understand a business situation, choose the right intervention, and, when technology is part of the answer, describe it through System Design.',
    images: ['/og.png'],
  },
  twitter: {
    card: 'summary_large_image',
    images: ['/og.png'],
  },
};

export default function Layout({ children }: LayoutProps<'/'>) {
  return (
    <html
      lang="en"
      className={inter.className}
      suppressHydrationWarning
      data-scroll-behavior="smooth"
    >
      <body className="flex flex-col min-h-screen">
        <RootProvider>{children}</RootProvider>
        <SpeedInsights />
        <Analytics />
      </body>
    </html>
  );
}

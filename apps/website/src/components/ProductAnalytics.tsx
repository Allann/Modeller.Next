'use client';

import { usePathname } from 'next/navigation';
import { useEffect } from 'react';
import { capture, normalizedRoute } from '@/lib/productAnalytics';

export function ProductAnalytics() {
  const pathname = usePathname();
  useEffect(() => capture('site_page_viewed', { route: normalizedRoute(pathname) }), [pathname]);
  return null;
}

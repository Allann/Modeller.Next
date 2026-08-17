'use client';

import { usePathname } from 'next/navigation';
import { useEffect } from 'react';
import { capture } from '@/lib/productAnalytics';

export function ProductAnalytics() {
  const pathname = usePathname();
  const route = pathname.startsWith('/docs/') ? '/docs/:article' : pathname;

  useEffect(() => {
    capture('site_page_viewed', { route });
    if (route === '/docs/:article') capture('docs_article_viewed', { route });
  }, [route]);

  useEffect(() => {
    let searchCaptured = false;
    const onInput = (event: Event) => {
      if (searchCaptured || !(event.target instanceof HTMLInputElement)) return;
      const label = `${event.target.type} ${event.target.placeholder} ${event.target.getAttribute('aria-label')}`.toLowerCase();
      if (!label.includes('search')) return;
      searchCaptured = true;
      capture('docs_search_used', { action: 'search' });
      capture('meaningful_use_started', { action: 'docs_search' });
    };
    const onClick = (event: MouseEvent) => {
      if (!(event.target instanceof Element)) return;
      const anchor = event.target.closest('a');
      if (!anchor) return;
      const target = new URL(anchor.href, window.location.href);
      if (target.hostname.endsWith('modeller.website') && target.hostname !== window.location.hostname) {
        capture('docs_call_to_action_selected', { action: 'modeller_site' });
        capture('meaningful_use_started', { action: 'docs_call_to_action' });
      } else if (target.origin !== window.location.origin) {
        capture('outbound_link_followed', { action: 'external' });
      }
    };
    document.addEventListener('input', onInput);
    document.addEventListener('click', onClick);
    return () => { document.removeEventListener('input', onInput); document.removeEventListener('click', onClick); };
  }, []);

  return null;
}

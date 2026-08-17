'use client';
import { useEffect } from 'react';
import { capture } from '@/lib/productAnalytics';
export function ProductAnalytics() { useEffect(() => { capture('site_page_viewed', { route: '/playground' }); capture('playground_opened'); }, []); return null; }

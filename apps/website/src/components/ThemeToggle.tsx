'use client';

import { useEffect, useState } from 'react';
import { useTheme } from 'next-themes';

function SunIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41" />
    </svg>
  );
}

function MoonIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
      <path d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 1020.354 15.354z" />
    </svg>
  );
}

export function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);

  // The server can never know the client's persisted theme preference, but
  // next-themes can resolve `resolvedTheme` synchronously on the client's very
  // first render (before hydration completes) — checking `resolvedTheme` itself
  // is not a reliable "haven't mounted yet" signal and caused a hydration
  // mismatch. An explicit `mounted` flag, only ever set true after mount, keeps
  // the first client render identical to the server's placeholder.
  // react-hooks/set-state-in-effect flags this, but there is no render-time alternative for a
  // mounted flag — it must stay false through the first client render and flip after, which is
  // exactly what an effect (not a render-time computation) is for.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => setMounted(true), []);
  if (!mounted) return <span className="theme-toggle" aria-hidden="true" />;

  const isDark = resolvedTheme === 'dark';
  return (
    <button
      type="button"
      className="theme-toggle"
      aria-label="Toggle Theme"
      title={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
      onClick={() => setTheme(isDark ? 'light' : 'dark')}
    >
      <span className={!isDark ? 'theme-option theme-option-active' : 'theme-option'}>
        <SunIcon />
      </span>
      <span className={isDark ? 'theme-option theme-option-active' : 'theme-option'}>
        <MoonIcon />
      </span>
    </button>
  );
}

import Link from 'next/link';
import { ThemeToggle } from './ThemeToggle';

export function SiteHeader() {
  return (
    <header className="site-header">
      <div className="site-header-inner">
        <Link href="/" className="site-header-brand">
          Modeller
        </Link>
        <nav className="site-header-nav">
          <a href="https://modeller.wiki/#why-modeller">Why Modeller</a>
          <a href="https://modeller.wiki/#journey">The journey</a>
          <a href="https://modeller.wiki/#system-design">System Design</a>
          <a href="https://modeller.wiki/docs">Docs</a>
          <Link href="/playground">Playground</Link>
          <Link className="site-header-cta" href="/playground">Start a Discovery Session</Link>
        </nav>
        <div className="site-header-tools">
          <ThemeToggle />
          <a
            className="github-link"
            href="https://github.com/Allann/Modeller.Next"
            aria-label="GitHub"
          >
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path fill="currentColor" d="M12 .7a11.5 11.5 0 0 0-3.64 22.41c.58.1.79-.25.79-.56v-2.02c-3.22.7-3.9-1.37-3.9-1.37-.53-1.34-1.29-1.7-1.29-1.7-1.05-.72.08-.7.08-.7 1.16.08 1.78 1.2 1.78 1.2 1.03 1.77 2.7 1.26 3.36.96.1-.75.4-1.26.73-1.55-2.57-.3-5.27-1.29-5.27-5.69 0-1.26.45-2.29 1.19-3.09-.12-.29-.52-1.46.11-3.05 0 0 .97-.31 3.16 1.18a10.94 10.94 0 0 1 5.76 0c2.2-1.49 3.16-1.18 3.16-1.18.63 1.59.23 2.76.11 3.05.74.8 1.19 1.83 1.19 3.09 0 4.42-2.71 5.39-5.29 5.68.42.36.79 1.07.79 2.16v3.03c0 .31.21.67.8.56A11.5 11.5 0 0 0 12 .7Z" />
            </svg>
          </a>
        </div>
      </div>
    </header>
  );
}

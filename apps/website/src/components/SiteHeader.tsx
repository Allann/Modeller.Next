import Link from 'next/link';
import { ThemeToggle } from './ThemeToggle';

export function SiteHeader() {
  return (
    <header className="site-header">
      <div className="site-header-inner">
        <Link href="/" className="site-header-brand">
          <span className="wordmark-mark">M</span>
          <strong>Modeller Playground</strong>
        </Link>
        <nav className="site-header-nav">
          <a href="https://modeller.wiki">Docs</a>
          <a href="https://modeller.wiki/getting-started">Install Modeller</a>
          <ThemeToggle />
        </nav>
      </div>
    </header>
  );
}

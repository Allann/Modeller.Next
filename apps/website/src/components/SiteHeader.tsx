import Link from 'next/link';

export function SiteHeader() {
  return (
    <header className="site-header">
      <div className="site-header-inner">
        <Link href="/" className="site-header-brand">
          Modeller Playground
        </Link>
        <nav className="site-header-nav">
          <a href="https://modeller.wiki">Docs</a>
          <a href="https://modeller.wiki/getting-started">Install Modeller</a>
        </nav>
      </div>
    </header>
  );
}

import Link from 'next/link';

export function SiteFooter() {
  return (
    <footer className="site-footer">
      <div className="site-footer-inner">
        <div className="site-footer-brand">
          <span className="wordmark-mark">M</span>
          <strong>Modeller</strong>
        </div>
        <p>Understand first. Change deliberately.</p>
        <nav aria-label="Footer navigation">
          <Link href="/examples">Browse examples</Link>
          <Link href="/playground">Try the interactive playground</Link>
          <a href="https://modeller.wiki/docs/getting-started">Get Modeller</a>
          <a href="https://modeller.wiki">Documentation</a>
          <Link href="/privacy">Privacy</Link>
        </nav>
      </div>
    </footer>
  );
}

import Link from 'next/link';

export function SiteFooter() {
  return (
    <footer className="site-footer">
      <div className="site-footer-inner">
        <div className="site-footer-brand">
          <span className="wordmark-mark">M</span>
          <strong>Modeller</strong>
        </div>
        <p>
          Start an Initiative, browse Modeller examples, or try the interactive playground — none of
          this is a substitute for local Modeller. Take a model further with:
        </p>
        <p>
          <a href="https://modeller.wiki/docs/getting-started">the CLI</a>
          <a href="https://modeller.wiki">local Studio</a>
          <a href="https://modeller.wiki/docs/getting-started">VS Code</a>
          <a href="https://modeller.wiki">modeller.wiki</a>
        </p>
        <p>
          <Link href="/examples">Browse examples</Link>
          <Link href="/playground">Try the interactive playground</Link>
          <Link href="/privacy">Privacy</Link>
        </p>
      </div>
    </footer>
  );
}

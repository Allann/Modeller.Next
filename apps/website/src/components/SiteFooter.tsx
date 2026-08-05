export function SiteFooter() {
  return (
    <footer className="site-footer">
      <div className="site-footer-inner">
        <div className="site-footer-brand">
          <span className="wordmark-mark">M</span>
          <strong>Modeller</strong>
        </div>
        <p>
          This is a public playground for exploring Modeller examples — not a substitute for local
          Modeller. Take a model further with:
        </p>
        <p>
          <a href="https://modeller.wiki/docs/getting-started">the CLI</a>
          <a href="https://modeller.wiki">local Studio</a>
          <a href="https://modeller.wiki/docs/getting-started">VS Code</a>
          <a href="https://modeller.wiki">modeller.wiki</a>
        </p>
        <p>
          <a href="/playground">Try the interactive playground</a>
          <a href="/privacy">Privacy</a>
        </p>
      </div>
    </footer>
  );
}

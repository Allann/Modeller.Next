# Modeller

This is the successor workspace for Modeller. During reconstruction the local
directory and repository use the temporary `Modeller.Next` label; the public
product remains **Modeller**.

Canonical project documentation lives in `docs/` and is rendered by the
Next.js/Fumadocs application in this repository.

## Development

```bash
npm install
npm run dev
```

For the complete development experience, open `Modeller.Next.code-workspace` in
VS Code. Run the `setup`, `docs: dev`, or `verify` tasks from the task picker.
Press F5 and choose **Modeller Docs** to start the development server and open
the site with browser debugging attached.

The existing `CSharp-Catalyst/Modeller` repository remains the legacy source of
implemented behaviour until the replacement gates in `MIGRATION.md` are met.

The temporary successor repository is hosted under `Allann/Modeller.Next`
because GitHub currently reports that the legacy `CSharp-Catalyst` organisation
is being deleted and will not accept new repositories.

---
title: Set up VS Code
description: Add RML highlighting, diagnostics, completion, and navigation.
---

# Set up VS Code

Install the **Modeller RML** extension from the VS Code Extensions view when it
is published. For a prerelease distributed as a VSIX:

```powershell
code --install-extension path/to/modeller-rml.vsix
```

Open the folder containing `.modeller/config.json`, then open a `.modeller`
file. The extension supplies syntax highlighting, indentation and folding,
diagnostics, completion, hover, go to definition, references, rename, document
symbols, and semantic tokens.

Packaged extensions either include the language server or require the
`modeller.languageServer.path` setting to point to a published
`Modeller.LanguageServer.dll`. Follow the release notes for the package you
installed; repository auto-detection is intended for contributors only.

To confirm activation, introduce an invalid keyword in a `.modeller` file. VS
Code should show a diagnostic. Remove it, save, and run the independent CLI
check:

```powershell
modeller validate model/context.modeller
```

Editor diagnostics shorten the feedback loop; CLI validation remains the
repeatable check for terminals and CI.

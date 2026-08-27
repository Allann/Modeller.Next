# QA procedure: Open a downloaded workspace in local Modeller Studio

This procedure proves that a non-developer reader can move from the wiki to the
hosted playground, download a workspace, and open it in local Modeller Studio
without cloning the repository or installing a developer toolchain.

## Setup

1. Use a clean Windows machine or Windows profile that does not have this
   repository cloned.
2. Have the public wiki landing page available.
3. Have the hosted playground available from the wiki path.
4. Have the first-slice Windows Modeller Studio installer available through the
   reader path.
5. Start with local Modeller Studio not installed, unless a step says
   otherwise.

## Procedure

### 1. Verify the primary reader path

1. Open the wiki landing page.
2. Find the local-use path.
3. Confirm the main path says, in effect: try the playground, download the
   workspace, install Studio for Windows, open the package, and see the
   workspace.
4. Confirm the main path does not use the words `clone`, `npm`, `dotnet`,
   `build`, `checkout`, `SDK`, or `package manager`.
5. Confirm any developer fallback is clearly separate from the main reader
   path.

### 2. Download the workspace package

1. Follow the wiki path to the hosted playground.
2. Use the sample workspace shown by the playground.
3. Choose the action that takes the workspace local.
4. Confirm one `.modeller-workspace` package is downloaded.
5. Inspect the package contents with normal archive tools.
6. Confirm it contains the source documents.
7. Confirm it contains `.modeller/config.json`.
8. Confirm it contains a durable identity registry when durable identities
   apply to the exported workspace.

### 3. Install Studio from the reader path

1. With Studio not installed, open the downloaded package or follow the
   open-locally path.
2. Confirm the reader is offered the Windows Studio installer.
3. Install Studio from that path.
4. Confirm the downloaded package is still available after installation.
5. Confirm no step asks the reader to clone the repository, open a terminal,
   install Node, install .NET, install an SDK, install a package manager, or
   choose a runtime.
6. Confirm Studio bundles or hides the required local runtimes and Modeller
   tools.

### 4. Open the downloaded workspace by file association

1. Find the `.modeller-workspace` package in Windows.
2. Confirm Windows shows it as a Modeller Studio workspace package.
3. Double-click the package, or use the normal Windows open action.
4. Confirm Studio starts if it is not already running.
5. Confirm Studio opens directly to that workspace.
6. Confirm the document list in Studio matches the document list from the
   hosted playground.
7. Confirm the document content matches the downloaded package content.
8. Confirm first launch asks for no technical choices before the workspace is
   visible.

### 5. Verify local diagnostics

1. With the downloaded workspace open in Studio, introduce a small source
   error in one document.
2. Confirm Studio reports a diagnostic without calling the hosted API.
3. Disconnect the network or block access to the hosted API.
4. Confirm diagnostics still run locally for the opened workspace.

### 6. Verify local saving

1. Change a source document in Studio.
2. Save the document.
3. Close and reopen the same workspace.
4. Confirm the saved change is present.
5. Confirm unchanged concepts keep stable identities.

### 7. Verify generation behaviour

1. Ask Studio to generate from the opened workspace.
2. Confirm generation runs from the opened workspace.
3. Confirm generation does not call the hosted API.
4. Confirm the generated artifacts match the opened workspace.
5. Confirm Studio does not ask the reader to install or choose a developer
   toolchain.

### 8. Verify humane failure states

1. Try to open a package that is not a valid workspace package.
2. Confirm Studio reports that the package cannot be opened in plain language.
3. Confirm any existing open workspace remains unchanged.
4. Try to open a package that declares an unsupported package version.
5. Confirm Studio explains that Studio must be updated or the package must be
   re-exported, as applicable.
6. Try to open a valid package into a folder blocked by file permissions.
7. Confirm Studio explains the permission problem and offers the simplest safe
   next action, such as choosing another folder.
8. Confirm none of these failures shows a raw exception or developer command as
   the main message.

### 9. Verify deterministic packaging and opening

1. Return to the hosted playground with the unchanged sample workspace.
2. Download the workspace twice.
3. Compare the logical files inside both packages.
4. Confirm the file paths, file contents, and package metadata that affects
   opening are the same.
5. Open each package in Studio.
6. Confirm Studio observes the same documents, configuration, identities,
   diagnostics, and generated artifacts for both packages.

## Pass criteria

The story passes only if the happy path is no more than: download workspace,
install Studio for Windows, open the package, see workspace. The primary reader
path must not require a repository clone, terminal command, Node installation,
visible .NET setup, SDK choice, package manager, or runtime choice.

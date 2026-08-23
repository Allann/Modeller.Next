# QA procedure: Load a local workspace into the Playground

Proves that a developer running the Playground on their own machine can load
real modeller documents from a local workspace directory — the child-care
sample, the ordering sample, or any other directory — instead of only the
bundled example, and that the public, hosted Playground is unaffected.

## Setup

1. Have a local checkout of this repository, including the `samples/child-care`
   and `samples/ordering` folders.
2. Run the Playground locally, in its normal (non-public) mode.

## Procedure

### 1. Load a real sample workspace

1. Open the Playground in a browser.
2. Point it at the `samples/child-care` workspace directory (however that
   directory is supplied — a field in the UI, a menu action, or a URL
   parameter).
3. Confirm the document list matches the files under
   `samples/child-care/model` on disk (open one or two of those files in a
   text editor and compare their content against what the Playground shows).
4. Confirm the documents shown are **not** the same as what the Playground
   showed before this step (the previous bundled example).

### 2. Switch to a different local workspace

1. With the child-care workspace still loaded, point the Playground at the
   `samples/ordering` workspace directory instead.
2. Confirm the document list now matches the files under
   `samples/ordering/model` on disk, and the child-care documents are gone.
3. Confirm this happened without restarting the Playground.

### 3. Load a workspace outside the samples folder

1. Copy `samples/ordering` to a folder outside the repository (for example,
   your desktop).
2. Point the Playground at that copied folder.
3. Confirm the Playground loads its documents the same way it did for a
   sample workspace in step 1.

### 4. Edits stay local to the session until saved

1. With a local workspace loaded, edit one document's content in the
   Playground.
2. Open the same file on disk in a text editor. Confirm it still shows the
   original content, not your edit.
3. Save the document from the Playground.
4. Re-open the file on disk. Confirm it now matches your edit.

### 5. Pointing at a directory that isn't a workspace

1. Point the Playground at an empty folder, or any folder that has no
   workspace configuration.
2. Confirm the Playground reports that the folder isn't a recognised
   workspace, rather than crashing or showing an empty document list.
3. Confirm the workspace that was loaded before this step is still shown.

### 6. The public Playground is unaffected

1. Run the Playground in its public, hosted mode (as an anonymous visitor
   would use it).
2. Confirm there is no way, in the UI, to point that instance at a local
   directory.
3. If you can call the underlying request directly (for example with a
   browser's network tools) asking it to load a local directory, confirm it
   is refused and the visitor's session keeps working from its normal
   bundled example, not left broken.

## Pass criteria

All six sections behave as described. In particular: the public, hosted
Playground never reads from any local directory, under any input — this is a
safety property, not just a feature, and step 6 is the one that proves it.

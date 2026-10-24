# QA procedure: Open generated code in a preferred IDE

This procedure proves that Studio hands generated code to the reader's normal
IDE. Studio remains responsible for workspace authoring and generation only.

## Setup

1. Install Modeller Studio for Windows.
2. Install an IDE that Windows can use to open the generated project entry
   point, such as VS
   Code, Rider, or Visual Studio.
3. Make that IDE the default Windows application for solution files.
4. Open a valid Modeller workspace in Studio.

## Procedure

### 1. Generate and open the output folder

1. Generate code from the workspace.
2. Choose **Open generated folder**.
3. Confirm that Windows opens the generated output folder.
4. Confirm that the folder contains the generated project and solution files.

### 2. Open the generated project

1. Return to Studio.
2. Choose **Open generated project in IDE**.
3. Confirm that Windows opens the template pack's generated project entry point in the configured default
   IDE.
4. Confirm that the reader can edit the generated code in that IDE.
5. Confirm that Studio did not ask the reader to select, install, build, test,
   or run anything.

### 3. Check the no-output state

1. Open another valid workspace that has not generated code yet.
2. Choose **Open generated folder** or **Open in IDE**.
3. Confirm that Studio says that code must be generated first.
4. Confirm that the open workspace remains available.

### 4. Check a pack without a generated project entry point

1. Open a workspace with a valid template pack that produces code but no
   declared project entry point.
2. Generate code.
3. Choose **Open generated project in IDE**.
4. Confirm that Studio explains that there is no generated project entry point to open.
5. Confirm that **Open generated folder** remains available.

## Pass criteria

After generation, the reader can open the generated folder or open the
generated project entry point in the default Windows IDE. Studio does not build, test, or
run generated code in this phase.

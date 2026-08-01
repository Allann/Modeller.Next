---
title: "Automatic Code Generation on Definition Changes"
---

# Automatic Code Generation on Definition Changes

This guide explains how to make Modeller feel closer to source generators by running generation automatically when definition files change.

The recommended approach is:

1. Run `modeller generate` automatically during build.
2. Use an incremental MSBuild target so generation only runs when DSL files changed.
3. Enable it only in the project that owns generation (usually SDK/Domain), not every project in the solution.

## Why This Approach

- Predictable: generated files are always up to date before compile.
- Safe: only `.g.*` files are overwritten.
- Scalable: avoids duplicate generation in multi-project solutions.

## Prerequisites

1. A Modeller project initialized with `.modeller/config.yaml`.
2. `modeller generate` works manually from your chosen working folder.
3. Modeller CLI installed either as a global tool or local tool.

Example manual check:

```bash
modeller generate --dry-run
```

## Step 1: Create Shared Build Properties

Create `Directory.Build.props` in the generated solution root:

```xml
<Project>
  <PropertyGroup>
    <!-- Opt-in switch: only projects that set this to true will run generation -->
    <EnableModellerGeneration Condition="'$(EnableModellerGeneration)' == ''">false</EnableModellerGeneration>

    <!-- CLI command to run (override per environment if needed) -->
    <ModellerCommand Condition="'$(ModellerCommand)' == ''">modeller</ModellerCommand>

    <!-- Folder that contains .modeller/config.yaml -->
    <ModellerWorkingDirectory Condition="'$(ModellerWorkingDirectory)' == ''">$(MSBuildProjectDirectory)</ModellerWorkingDirectory>

    <!-- Optional: choose a profile/layer from build properties -->
    <ModellerProfile Condition="'$(ModellerProfile)' == ''"></ModellerProfile>
    <ModellerLayer Condition="'$(ModellerLayer)' == ''"></ModellerLayer>
  </PropertyGroup>
</Project>
```

If your `.modeller` folder is at solution root (not project root), override `ModellerWorkingDirectory` in the relevant project (shown in Step 3).

## Step 2: Add a Shared Incremental Target

Create `Directory.Build.targets` in the generated solution root:

```xml
<Project>
  <ItemGroup Condition="'$(EnableModellerGeneration)' == 'true'">
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.def" />
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.entity" />
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.enum" />
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.flags" />
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.key" />
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.service" />
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.command" />
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.query" />
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.projection" />
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.event" />
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.value" />
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.shared" />
    <ModellerDsl Include="$(ModellerWorkingDirectory)/domain/**/*.union" />
  </ItemGroup>

  <PropertyGroup Condition="'$(EnableModellerGeneration)' == 'true'">
    <_ModellerStamp>$(ModellerWorkingDirectory)/.modeller/obj/$(MSBuildProjectName).modeller.stamp</_ModellerStamp>
    <_ModellerArgs>generate</_ModellerArgs>
    <_ModellerArgs Condition="'$(ModellerProfile)' != ''">$(_ModellerArgs) --profile $(ModellerProfile)</_ModellerArgs>
    <_ModellerArgs Condition="'$(ModellerLayer)' != ''">$(_ModellerArgs) --layer $(ModellerLayer)</_ModellerArgs>
  </PropertyGroup>

  <Target Name="ModellerGenerate"
          BeforeTargets="CoreCompile"
          Condition="'$(EnableModellerGeneration)' == 'true'"
          Inputs="@(ModellerDsl)"
          Outputs="$(_ModellerStamp)">
    <Message Importance="high" Text="Running Modeller generation in $(ModellerWorkingDirectory)" />
    <Exec Command="$(ModellerCommand) $(_ModellerArgs)" WorkingDirectory="$(ModellerWorkingDirectory)" />
    <MakeDir Directories="$(ModellerWorkingDirectory)/.modeller/obj" />
    <Touch Files="$(_ModellerStamp)" AlwaysCreate="true" />
  </Target>
</Project>
```

What this gives you:

- Build triggers generation before compile.
- MSBuild incremental behavior skips generation when no DSL inputs changed.
- Per-project stamp file avoids collisions.

## Step 3: Enable in the Owning Project Only

In the project that should own generation (for example `MyCompany.Sdk.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>

    <!-- Opt in -->
    <EnableModellerGeneration>true</EnableModellerGeneration>

    <!-- Set this if .modeller is in solution root -->
    <!-- <ModellerWorkingDirectory>$(MSBuildProjectDirectory)/..</ModellerWorkingDirectory> -->

    <!-- Optional profile used during build -->
    <!-- <ModellerProfile>default</ModellerProfile> -->
  </PropertyGroup>
</Project>
```

Do not enable generation in every project. One owner project is usually enough.

## Step 4: Validate

Run from solution root:

```bash
dotnet build
```

Expected behavior:

1. First build runs Modeller generation.
2. Second build with no DSL changes skips Modeller generation.
3. Editing a file under `domain/` runs generation again on next build.

## Optional: Local Watch Workflow in VS Code

If you want near-instant regeneration while editing, add a watch task and keep build-time generation as the source of truth.

Example `.vscode/tasks.json` snippet:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "modeller: generate",
      "type": "shell",
      "command": "modeller generate",
      "options": {
        "cwd": "${workspaceFolder}"
      },
      "problemMatcher": []
    }
  ]
}
```

Use any file-watch mechanism your team prefers (VS Code extension, external watcher, task runner) to trigger this task on save.

## CI Recommendation

No special CI step is required if you use the target above.

```bash
dotnet build
```

CI will run the same generation path as local builds.

## Troubleshooting

### Generation never runs

- Check `EnableModellerGeneration` is `true` in the owning project.
- Check `ModellerWorkingDirectory` points to the folder containing `.modeller/config.yaml`.
- Ensure `modeller generate` works manually in that folder.

### Generation runs in too many projects

- Ensure only one project sets `EnableModellerGeneration=true`.

### `modeller` command not found

- Install global tool or set `ModellerCommand` to your local command path.
- Alternatively use `dotnet tool run modeller` if you keep a local tool manifest.

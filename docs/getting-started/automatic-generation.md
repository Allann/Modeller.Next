---
title: Automatic generation
description: Regenerate output incrementally when RML definitions change.
---

# Automatic generation

First confirm that `modeller generate --workspace .` succeeds manually. Then
add this target to the one project that owns generated output:

```xml
<Project>
  <PropertyGroup>
    <ModellerWorkspace>$(MSBuildThisFileDirectory)</ModellerWorkspace>
    <ModellerStamp>$(BaseIntermediateOutputPath)modeller/generation.stamp</ModellerStamp>
  </PropertyGroup>

  <ItemGroup>
    <ModellerInput Include="$(ModellerWorkspace)model/**/*.modeller" />
    <ModellerInput Include="$(ModellerWorkspace).modeller/config.json" />
    <ModellerInput Include="$(ModellerWorkspace).modeller/identities.json" />
    <ModellerInput Include="$(ModellerWorkspace)templates/**/*" />
  </ItemGroup>

  <Target Name="ModellerGenerate"
          BeforeTargets="CoreCompile"
          Inputs="@(ModellerInput)"
          Outputs="$(ModellerStamp)">
    <Exec Command="modeller generate --workspace &quot;$(ModellerWorkspace)&quot;" />
    <MakeDir Directories="$([System.IO.Path]::GetDirectoryName('$(ModellerStamp)'))" />
    <Touch Files="$(ModellerStamp)" AlwaysCreate="true" />
  </Target>
</Project>
```

If the repository uses a local tool manifest, change the command to
`dotnet tool run modeller generate ...`. Adjust `ModellerWorkspace` when the
owning project is not in the workspace root.

Verify the incremental behavior:

1. Build once; generation runs.
2. Build again without edits; MSBuild skips generation.
3. Change a `.modeller` file; the next build regenerates before compilation.

Enable this target in only one project to avoid concurrent writers. In CI, run
`dotnet tool restore` first when using a local tool, then use the normal build.
The same ownership and conflict checks used during manual generation still
apply.

For an optional edit-time loop, configure a file watcher to run the same CLI
command on `.modeller` changes. Keep the incremental build target as the
authoritative check so generation does not depend on an editor being open.

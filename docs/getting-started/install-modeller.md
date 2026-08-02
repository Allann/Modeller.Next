---
title: Install Modeller
description: Install Modeller as a global or repository-local .NET tool.
---

# Install Modeller

Modeller is distributed as the `Modeller.Cli` .NET tool and exposes the
`modeller` command. Install it globally for interactive use:

```powershell
dotnet tool install --global Modeller.Cli
modeller --help
```

Update or remove it with:

```powershell
dotnet tool update --global Modeller.Cli
dotnet tool uninstall --global Modeller.Cli
```

For a team or CI pipeline, pin a version in the repository instead:

```powershell
dotnet new tool-manifest
dotnet tool install Modeller.Cli
dotnet tool restore
dotnet tool run modeller --help
```

Use `dotnet tool run modeller` wherever the remaining guides show `modeller`.

## Prerelease or private package feed

If the package is not on NuGet.org, add the feed supplied with the release:

```powershell
dotnet tool install --global Modeller.Cli --add-source <package-source>
```

Package maintainers can test a locally packed build with:

```powershell
dotnet pack src/Modeller.Cli -c Release -o artifacts/packages
dotnet tool install --global Modeller.Cli --add-source artifacts/packages
```

The last example is contributor/release verification, not the normal user
installation path.

---
title: Issue 95 .NET SDK 10.0.400 upgrade research
description: Compatibility and version policy for moving Modeller.Next from SDK 10.0.3xx to 10.0.400 and runtime 10.0.11.
---

Date: 2026-08-17

## Question

What must Modeller.Next check and change when it moves from .NET SDK 10.0.302 to SDK 10.0.400 and the .NET 10.0.11 servicing release?

## Decision summary

Proceed with the upgrade. Use SDK `10.0.400` and runtime or ASP.NET Core version `10.0.11`. Treat the SDK feature-band change as a controlled toolchain change. Do not make application compatibility edits unless a build, test, generated-file comparison, or container check identifies a failure.

Use these version rules:

- Set `global.json` to `10.0.400` with `rollForward: latestPatch`. This accepts later `10.0.4xx` servicing SDKs, but it does not cross into another feature band.
- Use `mcr.microsoft.com/dotnet/sdk:10.0.400` in both Dockerfiles.
- Use `mcr.microsoft.com/dotnet/aspnet:10.0.11` in both Dockerfiles. Do not use the floating `10.0` tag for a release build.
- Update the centrally managed `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.AspNetCore.OpenApi`, and `Microsoft.AspNetCore.SignalR.Client` packages from `10.0.10` to `10.0.11`.
- Keep the two Dockerfiles synchronized. For reproducible production images, record the resolved image digests in the pull request or pin each `FROM` line by digest after the first verified build.

The official release metadata identifies .NET `10.0.11` and SDK `10.0.400` as the current .NET 10 release, dated 11 August 2026. The release is a security release. .NET 10 is active LTS and ends support on 14 November 2028 ([release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)).

## Security release

The 10.0.11 release notes list ten security fixes. They include remote-code-execution, elevation-of-privilege, information-disclosure, denial-of-service, and security-feature-bypass vulnerabilities. The listed identifiers are CVE-2026-62898, CVE-2026-62899, CVE-2026-62900, CVE-2026-62901, CVE-2026-62886, CVE-2026-62871, CVE-2026-70354, CVE-2026-62902, CVE-2026-62897, and CVE-2026-62909 ([.NET 10.0.11 release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.11/10.0.11.md)).

This makes the runtime image update material. Updating only the SDK build image does not update the framework-dependent API runtime. The final ASP.NET image must also move to 10.0.11.

The release note does not list a release-specific known-issues section. The separate .NET 10 known-issues page does list active issues. The directly relevant issue is up to 10% slower startup for x64 containers with fractional CPU allocation. Vercel cold-start checks must measure this. Configuration binding can also throw when an empty array binds to an uninitialized `IEnumerable<T>`, `IReadOnlyList<T>`, or `IReadOnlyCollection<T>` property. The repository's current options should be checked for these shapes. Other listed issues affect macOS `createdump`, Debian 13 package installation, and workload-manifest updates; they do not affect the current Docker build path ([.NET 10 known issues](https://github.com/dotnet/core/blob/main/release-notes/10.0/known-issues.md)).

## Feature-band assessment

SDK `10.0.400` is a new feature band. `global.json` defines the third version group as the feature band. The `latestPatch` rule stays in the requested feature band. Therefore, the current `10.0.302` plus `latestPatch` rule cannot select `10.0.400` ([global.json reference](https://learn.microsoft.com/dotnet/core/tools/global-json)).

Microsoft maps SDK `10.0.3xx` to MSBuild and Visual Studio 18.6, and SDK `10.0.4xx` to 18.9. The 10.0.4xx band is the final .NET 10 SDK feature band and has support through the .NET 10 runtime lifecycle. SDK `10.0.400` requires Visual Studio 2026 18.9 when Visual Studio hosts the SDK. It can target `net10.0`; `dotnet build` remains the preferred command for consistent tooling ([SDK, MSBuild, and Visual Studio versioning](https://learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs)).

The official SDK release history contains these areas that are relevant to this repository ([SDK 10.0.400 release](https://github.com/dotnet/sdk/releases/tag/v10.0.400)):

- MSBuild server handling changed so the CLI no longer disables `MSBUILDUSESERVER`. Compare clean and incremental builds if an agent or CI job shows stale output.
- `dotnet test` received Microsoft.Testing.Platform solution parsing, positional argument, process-exit, progress display, and working-directory fixes. This repository still uses `Microsoft.NET.Test.Sdk` with xUnit v3, so it must keep its current runner model unless a deliberate migration is made. Run the complete solution tests, not only the API tests.
- SDK analyzer inputs and analyzer fixes changed. `TreatWarningsAsErrors` is enabled in `Directory.Build.props`, so any new diagnostic is a build blocker. Do not suppress a new warning until its cause is reviewed.
- NuGet command forwarding and restore workarounds changed. Restore must complete without new unexplained warnings. The repository does not commit NuGet lock files, so `rollForward: latestPatch` is consistent with its present dependency policy.
- Container publishing received digest-related changes. They apply to SDK container publishing. This repository uses hand-written Dockerfiles, so they do not directly change its build, but digest verification remains useful.
- Many 10.0.400 changes concern file-based apps, workloads, `dotnet watch`, and .NET tools. The repository uses normal project files and no workload manifest, so these changes have no identified direct impact.

The release page includes merged servicing fixes and feature-band history. A listed change is not evidence that Modeller.Next uses that feature. The upgrade should use failures and artifact diffs to decide whether compatibility code is necessary.

## Runtime and ASP.NET Core assessment

The application continues to target `net10.0`. A feature-band change changes the SDK, MSBuild, compiler, analyzers, and bundled tools. It does not change the target framework name. The .NET and ASP.NET Core breaking-change catalogues are mainly major-version migration guidance, and this repository already targets .NET 10 ([.NET 10 compatibility catalogue](https://learn.microsoft.com/dotnet/core/compatibility/10/), [ASP.NET Core 10 breaking changes](https://learn.microsoft.com/aspnet/core/breaking-changes/10/overview)).

The repository areas that deserve focused regression checks are:

- `System.Text.Json` contracts and canonical JSON output. Many source projects serialize persisted or generated data, and committed fixtures can detect output drift.
- Minimal API request binding and malformed-request responses.
- OpenAPI generation through `Microsoft.AspNetCore.OpenApi` and the direct `Microsoft.OpenApi` compatibility pin.
- SignalR negotiation, CORS headers, and the two-client update test.
- `WebApplicationFactory` integration tests after `Microsoft.AspNetCore.Mvc.Testing` moves to 10.0.11.
- Health endpoints and framework-dependent startup in the 10.0.11 ASP.NET runtime image.

Two ASP.NET Core 10 behavior changes deserve explicit checks, although they were introduced with the major version rather than this feature band. Cookie authentication returns 401 or 403 instead of redirecting for known API endpoints, and handled `IExceptionHandler` exceptions suppress diagnostics by default. This API does not currently use cookie authentication, and no reviewed code indicates an `IExceptionHandler` dependency, so neither requires an upgrade edit ([ASP.NET Core 10 breaking changes](https://learn.microsoft.com/aspnet/core/breaking-changes/10/overview?view=aspnetcore-10.0)).

No reviewed catalogue entry requires a source edit before these checks. Preserve that result: do not mix unrelated refactoring into this security and toolchain update.

## Container and CI policy

Official .NET image tags encode the image type, .NET version, operating system, architecture, and optional image variant. A short tag such as `aspnet:10.0` follows servicing updates. A full patch tag such as `aspnet:10.0.11` fixes the .NET version, but Microsoft can rebuild it with updated OS packages. Fixed tags normally receive image-policy rebuilds for about one month, and exceptional component updates can produce suffixed tags such as `-1`. A digest is required for an exact OS image identity ([container image tagging scheme](https://learn.microsoft.com/dotnet/core/docker/container-images), [supported .NET image tags](https://github.com/dotnet/dotnet-docker/blob/main/documentation/supported-tags.md)).

Use this project policy:

1. `global.json`, both SDK build images, and CI must resolve the same accepted SDK feature band.
2. Both runtime images must name the matching supported servicing patch.
3. A dependency update may advance `10.0.400` to a later `10.0.4xx` SDK and `10.0.11` to a later .NET 10 patch only as one reviewed change.
4. Production builds should use recorded digests when the host supports them. Refresh the digest only with the named-version update or with a documented base-image rebuild review.
5. CI currently installs floating `10.0.x`. Change it to `10.0.400` or make it consume `global.json`, then print `dotnet --info`. Otherwise CI can test a different feature band from the Docker build.

This policy prevents the current mismatch: the Docker build image is exact at `10.0.302`, `global.json` cannot leave 10.0.3xx, CI floats across .NET 10 SDKs, and the runtime image floats across .NET 10 servicing releases.

## Repository change map

| File | Required change or check |
| --- | --- |
| `global.json` | Set SDK to `10.0.400`; keep `latestPatch`. |
| `Dockerfile.vercel` | Set SDK to `10.0.400` and ASP.NET runtime to `10.0.11`; keep it equal to the API Dockerfile. |
| `src/Modeller.Api/Dockerfile` | Set SDK to `10.0.400` and ASP.NET runtime to `10.0.11`. |
| `Directory.Packages.props` | Set the three `Microsoft.AspNetCore.*` packages to `10.0.11`. Keep unrelated packages unchanged unless restore proves a conflict. |
| `.github/workflows/dotnet-tests.yml` | Replace floating `10.0.x` selection with the accepted SDK policy and report `dotnet --info`. |
| `.github/workflows/api-container.yml` | Make the same CI SDK change. Build both Dockerfiles from a cold cache. |
| Generated and expected files | Compare after build and test. Accept changes only when the release evidence explains them. |

## Verification gate

Run these checks after the version-only edit:

1. Confirm that `dotnet --info` resolves SDK 10.0.400 from the repository root.
2. Restore and build `Modeller.slnx` with no new unexplained warning.
3. Run all solution tests. Pay special attention to JSON fixtures, OpenAPI, malformed requests, CORS, SignalR, and `WebApplicationFactory` tests.
4. Compare generated artifacts and committed snapshots.
5. Build both Dockerfiles from a cold image cache.
6. Run the API image and check `/healthz/live`, `/healthz/ready`, and an Initiative create/load round trip.
7. Record the exact base-image digests used by the verified build.
8. Verify a Vercel Preview deployment before production promotion.

If a check fails, link the failure to an official release change or a minimal reproduction before editing application behavior.

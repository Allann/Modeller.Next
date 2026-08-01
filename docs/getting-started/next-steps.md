---
title: "Modeller — Next Steps"
---

# Modeller — Next Steps

_Last updated: 22 May 2026_

---

## Completed

| # | Task | Done |
|---|---|---|
| 1 | Commit line-ending cleanup (.gitattributes) | ✅ |
| 2 | Update stale implementation status doc | ✅ |
| 3 | Verify end-to-end generation workflow | ✅ |
| 4 | Expand integration test coverage | ✅ 169 tests (84 integration) |
| 5 | Set up CI pipeline | ✅ ci.yml + release.yml |
| 8 | Prepare VS Code extension for Marketplace | ✅ v1.4.0 packaged, workflow wired |

---

## Remaining

### 6. Publish the NuGet Global Tool

Intentionally deferred. The `Modeller.Cli.csproj` is already configured as a global tool. When ready:

1. Obtain a NuGet API key
2. Add it as a `NUGET_API_KEY` secret in GitHub
3. Add a publish step to `release.yml`:
   ```yaml
   - name: Push to NuGet
     run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
   ```
4. Tag a release: `git tag v1.4.2 && git push origin v1.4.2`

---

### 7. Implement Workflow DSL Support (4–8 hrs)

The architecture docs describe `workflow` as a first-class concept but there is no parser or generator support. If workflows are needed for target domains:

1. Add `WorkflowNode` to `Ast/BehaviourNodes.cs`
2. Add `WorkflowParsers.cs` following the pattern of `BehaviourParsers.cs`
3. Extend `DomainBuilder` to build workflow domain objects
4. Add Scriban templates for workflow scaffolding

---

### 8. Publish the VS Code Extension

The extension is packaged and the `vscode-extension.yml` workflow is ready. Manual prerequisite:

1. Create a publisher account at [marketplace.visualstudio.com](https://marketplace.visualstudio.com) with publisher ID `catalyst`
2. Generate a Personal Access Token
3. Add it as a `VSCE_PAT` secret in GitHub
4. Tag a release: `git tag vscode-v1.4.0 && git push origin vscode-v1.4.0`

---

### 9. Add a Second Language Template Pack (ongoing)

The architecture is language-agnostic but only C# templates exist. A TypeScript/Node.js pack (or SQL schema generator) would demonstrate the multi-language design. The `_shared/` and `_snippets/` directories are already in place for cross-language fragments.

---

### 10. Document the Template Authoring Guide (2–3 hrs)

There is no guide for developers who want to write new template packs. A concise guide covering:

- Structure of `pack.yaml` and `template.yaml`
- Available Scriban model variables (domain, entity, service, etc.)
- Custom template functions (`DomainTemplateFunctions`)
- How to test a template locally

---

## Priority Summary

| # | Task | Effort | Impact |
|---|---|---|---|
| 6 | Publish NuGet global tool | 30 min | Distribution |
| 7 | Workflow DSL support | 4–8 hrs | DSL completeness |
| 8 | Publish VS Code extension | 30 min (prereq: publisher account) | Developer experience |
| 9 | Second language template pack | Ongoing | Breadth |
| 10 | Template authoring guide | 2–3 hrs | Contributor enablement |

---
title: .NET API map
description: The implemented public module entry points and their responsibilities.
---

# .NET API map

Modeller exposes focused .NET modules rather than one plugin or fluent-builder
API. Callers compose the smallest interfaces needed for their workflow.

| Assembly | Primary entry point | Responsibility |
| --- | --- | --- |
| `Modeller.Model` | `CanonicalModel.Apply` | Typed changes to canonical definitions |
| `Modeller.Contexts` | `ContextPackageSystem` | Canonical package persistence, migration, and federation |
| `Modeller.Parsing` | `DefinitionParser.Parse` | Readable source to canonical model with provenance |
| `Modeller.Validation` | `SemanticValidation.Validate` | Staged semantic validation |
| `Modeller.Rules` | `RulesRuntime` | Bind and evaluate rules and decision tables |
| `Modeller.Projections` | `DiagramProjector`, `ProjectionEditor` | Project views and translate edits |
| `Modeller.Configuration` | `ConfigurationResolver.Resolve` | Deterministic layered configuration |
| `Modeller.Templates` | `TemplatePackLoader.Load` | Validate and normalise template packs |
| `Modeller.Generation` | `GenerationPlanner.Plan` | Pure deterministic generation planning |
| `Modeller.Rendering` | `TemplateRenderer.RenderAsync` | Render proposed artifacts through `IRendererAdapter` |
| `Modeller.Output` | `OutputApplication.ExecuteAsync` | Preview or apply manifest-owned output |
| `Modeller.GenerationWorkflow` | `GenerationExecution.ExecuteAsync` | Orchestrate plan, render, and output |
| `Modeller.Conformance` | `ConformanceRunner.RunAsync` | Execute contract fixtures and evidence checks |
| `Modeller.Editor` | `EditorIntegration.ExecuteAsync` | Version-aware editor workflows |
| `Modeller.Cli` | `CliApplication.RunAsync` | System.CommandLine workflow adapter |

The reference pages preceding this map document each contract's inputs,
invariants, diagnostics, determinism, and security boundaries. Public records are
immutable request/result values; side effects are isolated behind adapter
interfaces such as `IRendererAdapter`, `IOutputFileSystem`, and `ICliHost`.

For an end-to-end composition, see
[generation plans](/docs/reference/generation-plans),
[template rendering](/docs/reference/template-rendering), and
[output application](/docs/reference/output-application).

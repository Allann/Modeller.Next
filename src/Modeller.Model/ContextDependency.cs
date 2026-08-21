namespace Modeller.Model;

public sealed record ContextDependency(
    SemanticId ImportingContextId,
    SemanticName ImportingContextName,
    SemanticId ExportingContextId,
    SemanticName ExportingContextName,
    SemanticId FactId,
    SemanticName FactName);

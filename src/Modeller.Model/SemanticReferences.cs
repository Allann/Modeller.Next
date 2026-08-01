namespace Modeller.Model;

public interface ISemanticReference
{
    SemanticId TargetId { get; }
}

public readonly record struct EntityReference(SemanticId TargetId) : ISemanticReference;
public readonly record struct FactReference(SemanticId TargetId) : ISemanticReference;
public readonly record struct LifecycleReference(SemanticId TargetId) : ISemanticReference;
public readonly record struct LifecycleStageReference(SemanticId TargetId) : ISemanticReference;
public readonly record struct OutcomeReference(SemanticId TargetId) : ISemanticReference;
public readonly record struct RuleReference(SemanticId TargetId) : ISemanticReference;
public readonly record struct ConclusionReference(SemanticId TargetId) : ISemanticReference;

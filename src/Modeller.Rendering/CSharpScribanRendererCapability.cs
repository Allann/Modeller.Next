using System.Collections.Immutable;
using Modeller.Model;
using Modeller.Templates;

namespace Modeller.Rendering;

public sealed class CSharpScribanRendererCapability : IRendererCapability
{
    public RendererIdentity Renderer => new("scriban", "1.0");
    public string Language => "csharp";
    public Func<string, string> NameForPath => CSharpTemplateNaming.Identifier;
    public ImmutableArray<string> RequiredParameterKeys => ["namespace", "targetFramework"];

    public bool TryValidateParameters(IReadOnlyDictionary<string, string> parameters, out string? diagnosticCode)
    {
        if (!this.HasAllRequiredParameters(parameters))
        {
            diagnosticCode = "workspace.configuration.csharp-parameters-invalid";
            return false;
        }
        diagnosticCode = null;
        return true;
    }

    public ITemplateGlobalsProvider CreateGlobalsProvider(AuthoredContextRevision revision, string projectName, IReadOnlyDictionary<string, string> parameters) =>
        new CSharpTemplateGlobalsProvider(revision, parameters["namespace"], projectName, parameters["targetFramework"]);
}

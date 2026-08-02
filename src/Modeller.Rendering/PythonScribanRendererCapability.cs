using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Modeller.Model;
using Modeller.Templates;

namespace Modeller.Rendering;

public sealed partial class PythonScribanRendererCapability : IRendererCapability
{
    public RendererIdentity Renderer => new("scriban", "1.0");
    public string Language => "python";
    public Func<string, string> NameForPath => PythonTemplateNaming.Identifier;
    public ImmutableArray<string> RequiredParameterKeys => ["packageName", "pythonVersion"];

    public bool TryValidateParameters(IReadOnlyDictionary<string, string> parameters, out string? diagnosticCode)
    {
        if (!this.HasAllRequiredParameters(parameters))
        {
            diagnosticCode = "workspace.configuration.python-parameters-invalid";
            return false;
        }
        if (!PackageNamePattern().IsMatch(parameters["packageName"]))
        {
            diagnosticCode = "workspace.configuration.python-package-name-invalid";
            return false;
        }
        if (!VersionPattern().IsMatch(parameters["pythonVersion"]))
        {
            diagnosticCode = "workspace.configuration.python-version-invalid";
            return false;
        }
        diagnosticCode = null;
        return true;
    }

    public ITemplateGlobalsProvider CreateGlobalsProvider(AuthoredContextRevision revision, string projectName, IReadOnlyDictionary<string, string> parameters) =>
        new PythonTemplateGlobalsProvider(revision, parameters["packageName"], projectName, parameters["pythonVersion"]);

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex PackageNamePattern();

    [GeneratedRegex(@"^3\.\d+$")]
    private static partial Regex VersionPattern();
}

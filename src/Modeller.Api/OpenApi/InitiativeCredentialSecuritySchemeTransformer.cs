using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Modeller.Api.Endpoints;

namespace Modeller.Api.OpenApi;

/// <summary>
/// Documents the app-specific credential scheme issue #146 introduced: the role-scoped Initiative
/// session credential carried in the <c>X-Initiative-Credential</c> header (an API-key-style header,
/// not OAuth2/bearer — see <c>InitiativeEndpoints.CredentialHeader</c>). Registers the security scheme
/// in the document's <c>components.securitySchemes</c> and applies it as a security requirement to
/// every <c>/v1/initiative/...</c> operation that actually reads the header, so Scalar (and any other
/// OpenAPI viewer) shows readers which calls need it and offers a place to enter one. Registered via
/// <c>AddOpenApi(options => options.AddDocumentTransformer&lt;...&gt;())</c> in Program.cs, alongside
/// <see cref="ExampleSchemaTransformer"/>.
/// </summary>
public sealed class InitiativeCredentialSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeId = "InitiativeCredential";

    /// <summary>The two routes under <c>/v1/initiative</c> that do not read the credential header —
    /// see <c>InitiativeEndpoints.MapInitiativeEndpoints</c>: agent-status is public/secret-free, and
    /// create is what mints the credential in the first place.</summary>
    private static readonly HashSet<(string Path, HttpMethod Method)> CredentialFreeOperations =
    [
        ("/v1/initiative/agent-status", HttpMethod.Get),
        ("/v1/initiative", HttpMethod.Post),
        ("/v1/initiative/", HttpMethod.Post),
    ];

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = InitiativeEndpoints.CredentialHeaderName,
            Description = "Role-scoped Initiative session credential (issue #146) minted by " +
                "POST /v1/initiative and returned once per role. Send the value exactly as minted; " +
                "the API infers the caller's role (Facilitator or Domain Expert) from the credential " +
                "itself, never from a client-supplied role.",
        };

        var schemeReference = new OpenApiSecuritySchemeReference(SchemeId, document, null);
        var requirement = new OpenApiSecurityRequirement { [schemeReference] = [] };

        foreach (var (path, pathItem) in document.Paths)
        {
            if (!path.StartsWith("/v1/initiative", StringComparison.Ordinal) || pathItem?.Operations is null)
                continue;

            foreach (var (method, operation) in pathItem.Operations)
            {
                if (CredentialFreeOperations.Contains((path, method)))
                    continue;

                operation.Security ??= [];
                operation.Security.Add(requirement);
            }
        }

        return Task.CompletedTask;
    }
}

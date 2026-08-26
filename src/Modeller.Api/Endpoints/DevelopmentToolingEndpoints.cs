using Scalar.AspNetCore;

namespace Modeller.Api.Endpoints;

/// <summary>Development-only API documentation surface — never mapped in Production.</summary>
public static class DevelopmentToolingEndpoints
{
    public static WebApplication MapDevelopmentTooling(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return app;

        app.MapOpenApi();
        app.MapScalarApiReference();
        return app;
    }
}

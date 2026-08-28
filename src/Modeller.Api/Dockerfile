# Portable, ordinary Kestrel HTTP server — no platform-specific runtime shape. Runs unmodified on
# Azure Container Apps, Google Cloud Run, or (via Dockerfile.vercel) Vercel container Functions.
# See docs/architecture/decisions/hosted-workspace-api.mdx.

FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props Modeller.slnx global.json ./
COPY src/Modeller.Api/Modeller.Api.csproj src/Modeller.Api/
COPY src/Modeller.Initiative/Modeller.Initiative.csproj src/Modeller.Initiative/
COPY src/Modeller.Initiative.OpenAICompatible/Modeller.Initiative.OpenAICompatible.csproj src/Modeller.Initiative.OpenAICompatible/
COPY src/Modeller.Workspace/Modeller.Workspace.csproj src/Modeller.Workspace/
COPY src/Modeller.Parsing/Modeller.Parsing.csproj src/Modeller.Parsing/
COPY src/Modeller.Configuration/Modeller.Configuration.csproj src/Modeller.Configuration/
COPY src/Modeller.Projections/Modeller.Projections.csproj src/Modeller.Projections/
COPY src/Modeller.Contexts/Modeller.Contexts.csproj src/Modeller.Contexts/
COPY src/Modeller.Validation/Modeller.Validation.csproj src/Modeller.Validation/
COPY src/Modeller.Model/Modeller.Model.csproj src/Modeller.Model/
COPY src/Modeller.Generation/Modeller.Generation.csproj src/Modeller.Generation/
COPY src/Modeller.Templates/Modeller.Templates.csproj src/Modeller.Templates/
COPY src/Modeller.Rendering/Modeller.Rendering.csproj src/Modeller.Rendering/
RUN dotnet restore src/Modeller.Api/Modeller.Api.csproj -r linux-x64 -p:PublishReadyToRun=true

# The embedded csharp/domain-project template pack (Modeller.Api.csproj's EmbeddedResource items)
# is sourced straight from the sample, not a duplicated copy — see that csproj's comment.
COPY samples/child-care/templates/csharp/domain-project/ samples/child-care/templates/csharp/domain-project/

COPY src/Modeller.Api/ src/Modeller.Api/
COPY src/Modeller.Initiative/ src/Modeller.Initiative/
COPY src/Modeller.Initiative.OpenAICompatible/ src/Modeller.Initiative.OpenAICompatible/
COPY src/Modeller.Workspace/ src/Modeller.Workspace/
COPY src/Modeller.Parsing/ src/Modeller.Parsing/
COPY src/Modeller.Configuration/ src/Modeller.Configuration/
COPY src/Modeller.Projections/ src/Modeller.Projections/
COPY src/Modeller.Contexts/ src/Modeller.Contexts/
COPY src/Modeller.Validation/ src/Modeller.Validation/
COPY src/Modeller.Model/ src/Modeller.Model/
COPY src/Modeller.Generation/ src/Modeller.Generation/
COPY src/Modeller.Templates/ src/Modeller.Templates/
COPY src/Modeller.Rendering/ src/Modeller.Rendering/
# ReadyToRun precompiles the assembly so the container isn't JIT-compiling from scratch on every
# cold start (this host scales to zero between requests — docs/architecture/decisions/hosted-workspace-api.mdx).
# RID/R2R are passed here rather than in the csproj so local Windows dev builds/tests are unaffected.
RUN dotnet publish src/Modeller.Api/Modeller.Api.csproj -c Release --no-restore -o /app \
    -r linux-x64 --self-contained false -p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS runtime
WORKDIR /app
COPY --from=build /app .

# The platform sets $PORT; Program.cs binds to it (falling back to 8080 for local runs).
ENV ASPNETCORE_HTTP_PORTS=""
EXPOSE 8080
ENTRYPOINT ["dotnet", "Modeller.Api.dll"]

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
RUN dotnet restore src/Modeller.Api/Modeller.Api.csproj

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
RUN dotnet publish src/Modeller.Api/Modeller.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS runtime
WORKDIR /app
COPY --from=build /app .

# The platform sets $PORT; Program.cs binds to it (falling back to 8080 for local runs).
ENV ASPNETCORE_HTTP_PORTS=""
EXPOSE 8080
ENTRYPOINT ["dotnet", "Modeller.Api.dll"]

# syntax=docker/dockerfile:1

# OpenPortal as one container: the API, the OpenID Connect provider and the built client.
#
#   docker build -t openportal .
#   docker compose up            (PostgreSQL, a one-shot migration step and the portal; see docker-compose.yml)
#
# The defaults run one instance on SQLite, migrating at startup; docker-compose.yml switches to PostgreSQL with a
# separate migration step. State lives in /data (Data Protection keys, token certificates and, with SQLite, the database): mount a volume
# there. The container listens on 8080 as the unprivileged "app" user.

# ---------------------------------------------------------------------------------------------------------------
# The client: built once here, copied into wwwroot below. The i18n check reads the server's resource files.
# ---------------------------------------------------------------------------------------------------------------
FROM node:22-alpine AS client
WORKDIR /src/src/OpenPortal.Web/ClientApp
COPY src/OpenPortal.Web/ClientApp/package.json src/OpenPortal.Web/ClientApp/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY src/OpenPortal.Web/ClientApp/ ./
COPY src/OpenPortal.Web/Resources/ ../Resources/
RUN npm run lint && npm run build

# ---------------------------------------------------------------------------------------------------------------
# The server. Restore first, from the project files alone, so a code change does not invalidate the package layer.
# global.json is left out on purpose: the image's SDK is the 10.0 band it was built from.
# ---------------------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/OpenPortal.SharedKernel/*.csproj src/OpenPortal.SharedKernel/
COPY src/OpenPortal.Client/*.csproj src/OpenPortal.Client/
COPY src/OpenPortal.Migrations.PostgreSql/*.csproj src/OpenPortal.Migrations.PostgreSql/
COPY src/OpenPortal.Web/*.csproj src/OpenPortal.Web/
COPY src/Modules/Identity/OpenPortal.Identity.Domain/*.csproj src/Modules/Identity/OpenPortal.Identity.Domain/
COPY src/Modules/Identity/OpenPortal.Identity.Application/*.csproj src/Modules/Identity/OpenPortal.Identity.Application/
COPY src/Modules/Identity/OpenPortal.Identity.Infrastructure/*.csproj src/Modules/Identity/OpenPortal.Identity.Infrastructure/
COPY src/Modules/Content/OpenPortal.Content.Domain/*.csproj src/Modules/Content/OpenPortal.Content.Domain/
COPY src/Modules/Content/OpenPortal.Content.Application/*.csproj src/Modules/Content/OpenPortal.Content.Application/
COPY src/Modules/Content/OpenPortal.Content.Infrastructure/*.csproj src/Modules/Content/OpenPortal.Content.Infrastructure/
COPY src/Modules/Access/OpenPortal.Access.Domain/*.csproj src/Modules/Access/OpenPortal.Access.Domain/
COPY src/Modules/Access/OpenPortal.Access.Application/*.csproj src/Modules/Access/OpenPortal.Access.Application/
COPY src/Modules/Access/OpenPortal.Access.Infrastructure/*.csproj src/Modules/Access/OpenPortal.Access.Infrastructure/
COPY src/Modules/Audit/OpenPortal.Audit.Domain/*.csproj src/Modules/Audit/OpenPortal.Audit.Domain/
COPY src/Modules/Audit/OpenPortal.Audit.Application/*.csproj src/Modules/Audit/OpenPortal.Audit.Application/
COPY src/Modules/Audit/OpenPortal.Audit.Infrastructure/*.csproj src/Modules/Audit/OpenPortal.Audit.Infrastructure/
RUN dotnet restore src/OpenPortal.Web/OpenPortal.Web.csproj

COPY src/ src/
RUN dotnet publish src/OpenPortal.Web/OpenPortal.Web.csproj \
      --configuration Release \
      --no-restore \
      --output /app \
      -p:SkipClientBuild=true \
      -p:UseAppHost=false
COPY --from=client /src/src/OpenPortal.Web/ClientApp/dist/ /app/wwwroot/

# ---------------------------------------------------------------------------------------------------------------
# Runtime: the ASP.NET Core image, no SDK, no Node.
# ---------------------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /data && chown app:app /data

COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__OpenPortal="Data Source=/data/openportal.db" \
    Database__MigrateOnStartup=true \
    DataProtection__KeysPath=/data/keys \
    Oidc__CertificatesPath=/data/oidc

USER app
EXPOSE 8080
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "OpenPortal.Web.dll"]

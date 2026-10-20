# syntax=docker/dockerfile:1
FROM node:24-bookworm-slim AS frontend
WORKDIR /build
COPY package.json pnpm-lock.yaml ./
# No dependency lifecycle scripts are required by this frontend toolchain.
RUN npm install --global --ignore-scripts --fetch-retries=5 --fetch-retry-mintimeout=1000 --fetch-retry-maxtimeout=10000 pnpm@11.25.0 \
    && pnpm install --frozen-lockfile --ignore-scripts
COPY frontend/ ./frontend/
COPY scripts/ ./scripts/
COPY src/ItAssetManagement.Api/wwwroot/ ./src/ItAssetManagement.Api/wwwroot/
RUN pnpm run build:production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /build
ARG BUILD_REVISION
ARG RENDER_GIT_COMMIT
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/
RUN dotnet restore src/ItAssetManagement.Api/ItAssetManagement.Api.csproj --locked-mode
COPY --from=frontend /build/artifacts/frontend-production/ ./artifacts/frontend-production/
RUN deployRevision="${BUILD_REVISION:-$RENDER_GIT_COMMIT}" \
    && dotnet publish src/ItAssetManagement.Api/ItAssetManagement.Api.csproj \
    --configuration Release --no-restore --output /publish \
    -p:FrontendPublishDirectory=/build/artifacts/frontend-production \
    -p:BuildRevision="$deployRevision" -p:RequireBuildRevision=true \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
COPY --from=backend /publish/ ./
USER $APP_UID
EXPOSE 8080
# Runtime only: no migration, database reset or seeding during deployment.
ENTRYPOINT ["dotnet", "ItAssetManagement.Api.dll"]

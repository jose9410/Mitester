# ─────────────────────────────────────────────────────────────────────────────
# STAGE 1: FRONTEND BUILD (Angular 18/19 Standalone)
# ─────────────────────────────────────────────────────────────────────────────
FROM node:20-alpine AS frontend-build
WORKDIR /app/client

# Instalar dependencias usando npm ci para builds reproducibles
COPY ClientApp/package*.json ./
RUN npm ci

# Copiar código fuente del frontend y compilar para producción
COPY ClientApp/ ./
RUN npm run build -- --configuration production

# ─────────────────────────────────────────────────────────────────────────────
# STAGE 2: BACKEND BUILD (.NET 8 SDK)
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

# Copiar solo el .csproj primero para aprovechar el cache de capas Docker
COPY ["MiTesterE2E.csproj", "./"]
RUN dotnet restore "MiTesterE2E.csproj"

# Copiar el resto del código fuente backend
COPY . .

# Copiar los artefactos generados de Angular a wwwroot para que ASP.NET Core los sirva
COPY --from=frontend-build /app/client/dist/frontend/browser ./wwwroot/

# Publicar la solución unificada en modo Release
RUN dotnet publish "MiTesterE2E.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ─────────────────────────────────────────────────────────────────────────────
# STAGE 3: RUNTIME UNIFICADO (Monolito Modular en ACA)
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Usuario no-root para cumplir con políticas de seguridad de Azure Container Apps
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Copiar los artefactos publicados (incluyendo wwwroot con Angular)
COPY --from=backend-build --chown=appuser:appuser /app/publish .

# Puerto estándar de ACA (HTTP interno; ACA gestiona TLS/HTTPS externamente)
EXPOSE 8080

# Variables de entorno por defecto
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "MiTesterE2E.dll"]

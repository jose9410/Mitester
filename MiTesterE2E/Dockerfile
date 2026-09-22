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
# STAGE 3: RUNTIME UNIFICADO (Monolito Modular con Playwright Chromium en ACA)
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Instalar dependencias nativas de Linux requeridas por Chromium Headless
USER root
RUN apt-get update && apt-get install -y --no-install-recommends \
    wget \
    ca-certificates \
    libnss3 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libpango-1.0-0 \
    libcairo2 \
    libasound2 \
    && rm -rf /var/lib/apt/lists/*

# Crear usuario no-root para ACA
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app

# Copiar los artefactos publicados
COPY --from=backend-build --chown=appuser:appuser /app/publish .

USER appuser

# Puerto estándar de ACA (HTTP interno; ACA gestiona TLS/HTTPS externamente)
EXPOSE 8080

# Variables de entorno por defecto
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PLAYWRIGHT_BROWSERS_PATH=/app/.playwright

ENTRYPOINT ["dotnet", "MiTesterE2E.dll"]

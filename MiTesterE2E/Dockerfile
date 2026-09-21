# ─────────────────────────────────────────────────────────────────────────────
# STAGE 1: BUILD
# Usa el SDK completo para compilar y publicar la aplicación.
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar solo el .csproj primero para aprovechar el cache de capas Docker
# (si solo cambia el código fuente, esta capa no se reconstruye)
COPY ["MiTesterE2E.csproj", "./"]
RUN dotnet restore "MiTesterE2E.csproj"

# Copiar el resto del código fuente (incluye schema-v1.1.json como EmbeddedResource)
COPY . .

# Publicar en modo Release, sin el SDK (self-contained = false, usa el runtime de la imagen base)
RUN dotnet publish "MiTesterE2E.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ─────────────────────────────────────────────────────────────────────────────
# STAGE 2: RUNTIME
# Usa únicamente el ASP.NET Core Runtime (imagen más liviana ~220MB vs ~900MB del SDK).
# Esta es la imagen que se despliega en Azure Container Apps (ACA).
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Usuario no-root para cumplir con políticas de seguridad de ACA
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Copiar los artefactos publicados desde el stage de build
COPY --from=build --chown=appuser:appuser /app/publish .

# Puerto estándar de ACA (HTTP interno; ACA gestiona TLS/HTTPS externamente)
EXPOSE 8080

# Variables de entorno por defecto (pueden sobreescribirse en ACA via Container App environment)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "MiTesterE2E.dll"]

# Mi Tester E2E (AutoTest E2E) — Backend Core

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-Web_API-512BD4?style=flat&logo=dotnet)](https://learn.microsoft.com/aspnet/core)
[![SignalR](https://img.shields.io/badge/SignalR-Real--Time_Telemetry-0078D4?style=flat&logo=azure-devops)](https://learn.microsoft.com/aspnet/core/signalr)
[![Docker](https://img.shields.io/badge/Docker-ACA_Ready-2496ED?style=flat&logo=docker&logoColor=white)](https://www.docker.com/)
[![Repository](https://img.shields.io/badge/GitHub-jose9410%2FMitester-181717?style=flat&logo=github)](https://github.com/jose9410/Mitester)

Núcleo Backend para la plataforma centralizada **"Mi Tester E2E"**, orientada a entornos de certificación de calidad en sistemas bancarios críticos (por ejemplo, conciliaciones masivas de hasta 1,500,000 transacciones).

Estructurado bajo un **Monolito Modular simplificado en .NET 8**, optimizado para ejecutarse en un único contenedor en **Azure Container Apps (ACA)** sin dependencias obligatorias de brokers externos en esta fase.

---

## 🏛️ Arquitectura del Sistema (Fase 1)

El backend desacopla la recepción HTTP de alta densidad del procesamiento masivo utilizando estructuras en memoria nativas y de alto rendimiento:

```
[Cliente Angular / REST] ─── HTTP POST ───> [OrchestrationController]
                                                       │
                                            (Validación JSON Schema)
                                            (NJsonSchema + Draft-07)
                                                       │
                                                       ▼
                                            [IExecutionTaskQueue]
                                            (System.Threading.Channels)
                                                       │
                                                       ▼
                                           [ExecutionBackgroundWorker]
                                              (BackgroundService)
                                                       │
                                          (Progreso y Finalización)
                                                       │
                                                       ▼
[Cliente Angular / WS]   <── WebSockets ─── [TelemetryHub (SignalR)]
```

### Componentes Clave:

1. **`OrchestrationController`**: Expone endpoints REST para validación de contratos y arranque asíncrono (retornando `HTTP 202 Accepted`).
2. **`NJsonSchema Validation Engine`**: Valida suites de prueba contra `schema-v1.1.json` (polimorfismo con `allOf` / `if-then` para `UI_SEQUENCE`, `SQL_EXECUTE`, `ASSERT_BUSINESS_RULES`). Carga eficiente en memoria mediante `Lazy<Task<JsonSchema>>` y recurso embebido (`EmbeddedResource`).
3. **`ExecutionTaskQueue` (`IExecutionTaskQueue`)**: Encapsula un `Channel<ExecutionTask>` con `SingleReader = true` para un traspaso thread-safe y ultra-rápido entre la API y el Worker.
4. **`ExecutionBackgroundWorker`**: `BackgroundService` continuo que consume tareas encoladas, ejecuta la orquestación simulada de lotes y publica telemetría en tiempo real.
5. **`TelemetryHub` (SignalR)**: Hub WebSocket para emitir eventos de progreso porcentual y notificaciones toast de finalización hacia el frontend sin incurrir en *polling* HTTP.

---

## 📋 Endpoints de la API (OpenAPI / Swagger)

| Método | Endpoint | Código HTTP | Descripción |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/orchestration/validate-schema` | `200 OK` | Valida un JSON de suite contra `schema-v1.1.json` devolviendo `isValid` y `validationErrors`. |
| `POST` | `/api/v1/orchestration/executions/start` | `202 Accepted` / `400 Bad Request` | Encola la ejecución de una suite tras validar su schema y retorna el `executionId`. |
| `WS` | `/hubs/telemetry` | `101 Switching Protocols` | Conexión WebSocket para telemetría en tiempo real. |

### Eventos de Telemetría (SignalR):
- **`ExecutionProgressUpdated`**: Transmite porcentaje completado (0-100%), paso activo y timestamp.
- **`ExecutionCompletedToast`**: Transmite estado final (`COMPLETED_SUCCESS`, `FAILED`), mensaje de resumen y duración en segundos.

---

## 🔒 Auditoría de Seguridad y Gestión de Secretos

El proyecto ha sido revisado conforme a las mejores prácticas de seguridad bancaria y Cloud Native:

- **Cero Credenciales en Código**: No existen contraseñas, tokens, llaves criptográficas ni cadenas de conexión quemadas en código fuente ni en archivos de configuración (`appsettings.json`).
- **Seguridad en Dockerfile**: El contenedor se ejecuta bajo un usuario no privilegiado (`appuser`), cumpliendo con los estándares de seguridad de **Azure Container Apps**.
- **Secretos en la Nube**: Cualquier referencia a credenciales de bases de datos (Oracle/SQL Server) debe resolverse mediante **Azure Key Vault** o variables de entorno inyectadas en tiempo de ejecución en ACA.
- **`.gitignore` Robusto**: Se excluyen automáticamente binarios compilados (`bin/`, `obj/`), secretos locales (`secrets.json`, `*.env`), certificados (`*.pfx`, `*.key`) y temporales de IDE.

---

## 🚀 Requisitos y Ejecución Local

### Prerrequisitos
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (opcional, para ejecución contenerizada)

### 1. Clonar el Repositorio
```bash
git clone https://github.com/jose9410/Mitester.git
cd Mitester
```

### 2. Restaurar dependencias y compilar
```bash
cd MiTesterE2E
dotnet restore
dotnet build -c Release
```

### 3. Ejecutar la API
```bash
dotnet run
```
La aplicación estará disponible por defecto en:
- **Swagger UI**: `http://localhost:5000` (o el puerto asignado en launchSettings / logs de consola).
- **Hub de SignalR**: `http://localhost:5000/hubs/telemetry`.

---

## 🐳 Construcción y Despliegue con Docker (Azure Container Apps)

El proyecto incluye un `Dockerfile` multi-stage optimizado para producción:

```bash
# Construir la imagen Docker
docker build -t mitester-backend:latest -f MiTesterE2E/Dockerfile MiTesterE2E/

# Ejecutar el contenedor localmente
docker run -d -p 8080:8080 --name mitester-app mitester-backend:latest
```

En **Azure Container Apps (ACA)**:
- Puerto objetivo de entrada (Ingress target port): `8080`
- Protocolo de transporte: `HTTP` (soporta WebSockets de forma nativa)

---

## 📂 Estructura del Repositorio

```text
├── .gitignore                     # Filtros de exclusión de artefactos y secretos
├── README.md                      # Documentación técnica principal
├── CONTEXTO_FASE1.md              # Requerimientos y arquitectura de negocio
├── schema-v1.1.json               # Contrato JSON Schema para suites E2E
├── swagger.yaml                   # Contrato OpenAPI 3.0 de endpoints REST
└── MiTesterE2E/                   # Proyecto Backend ASP.NET Core
    ├── MiTesterE2E.csproj         # Definición de dependencias (.NET 8, NJsonSchema)
    ├── Program.cs                 # Configuración de pipeline, CORS, SignalR e Inyección
    ├── appsettings.json           # Configuración de logs y cola en memoria
    ├── Dockerfile                 # Configuración multi-stage para ACA
    ├── schema-v1.1.json           # Recurso embebido para validación
    ├── Orchestration/
    │   ├── Contracts/             # DTOs de entrada y salida (OpenAPI)
    │   ├── Controllers/           # OrchestrationController (REST API)
    │   ├── Services/              # IExecutionTaskQueue y ExecutionTaskQueue (Channel)
    │   └── Workers/               # ExecutionBackgroundWorker (Procesamiento en segundo plano)
    └── Telemetry/
        ├── TelemetryHub.cs        # Hub de SignalR para WebSockets
        └── Contracts/             # Payloads tipados de eventos en tiempo real
```

---

## 👥 Equipo y Autoría
- **Proyecto**: Mi Tester E2E (AutoTest E2E)
- **Repositorio Oficial**: [https://github.com/jose9410/Mitester](https://github.com/jose9410/Mitester)

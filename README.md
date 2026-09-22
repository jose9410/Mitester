# Mi Tester E2E (AutoTest E2E) — Full Stack Platform

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Angular 18+](https://img.shields.io/badge/Angular-18%2F19_Standalone-DD0031?style=flat&logo=angular&logoColor=white)](https://angular.dev/)
[![Material 3](https://img.shields.io/badge/Material_Design-M3_Tokens-757575?style=flat&logo=materialdesign&logoColor=white)](https://material.angular.io/)
[![SignalR](https://img.shields.io/badge/SignalR-Real--Time_Telemetry-0078D4?style=flat&logo=azure-devops)](https://learn.microsoft.com/aspnet/core/signalr)
[![Docker](https://img.shields.io/badge/Docker-ACA_Ready-2496ED?style=flat&logo=docker&logoColor=white)](https://www.docker.com/)
[![Repository](https://img.shields.io/badge/GitHub-jose9410%2FMitester-181717?style=flat&logo=github)](https://github.com/jose9410/Mitester)

Plataforma unificada para la orquestación, certificación y triaje de pruebas End-to-End en sistemas bancarios críticos (conciliaciones masivas de hasta 1,500,000 transacciones).

---

## 🏛️ Arquitectura Global del Sistema

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           FRONTEND (Angular 18+)                           │
│                                                                             │
│  ┌───────────────────────┐  ┌─────────────────────┐  ┌───────────────────┐  │
│  │ Dashboard Scorecard   │  │ Triage Drawer (M3)  │  │ Dynamic Form Gen  │  │
│  │ (Signals + Threshold) │  │ (Log Extract / Diff)│  │ (schema-v1.1)     │  │
│  └──────────┬────────────┘  └──────────┬──────────┘  └─────────┬─────────┘  │
│             │                          │                       │            │
│             └──────────────────────────┼───────────────────────┘            │
│                                        ▼                                    │
│                           [TelemetryService (Signals)]                      │
└────────────────────────────────────────┬────────────────────────────────────┘
                                         │ HTTP REST & SignalR WS
                                         ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           BACKEND (.NET 8 Web API)                          │
│                                                                             │
│  ┌───────────────────────────────┐     ┌─────────────────────────────────┐  │
│  │ OrchestrationController       │     │ TelemetryHub (SignalR)          │  │
│  │ (POST /validate-schema,       │     │ (ExecutionProgressUpdated,      │  │
│  │  POST /executions/start)      │     │  ExecutionCompletedToast)       │  │
│  └──────────────┬────────────────┘     └────────────────▲────────────────┘  │
│                 │                                       │                   │
│                 ▼                                       │                   │
│  ┌───────────────────────────────┐                      │                   │
│  │ IExecutionTaskQueue (Channel) │                      │                   │
│  └──────────────┬────────────────┘                      │                   │
│                 ▼                                       │                   │
│  ┌──────────────────────────────────────────────────────┴────────────────┐  │
│  │ ExecutionBackgroundWorker (BackgroundService)                         │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 🎨 Módulos del Frontend (Angular 18+)

### 1. `DashboardScorecardComponent`
- **Signals y Reactividad**: `signal()`, `computed()`, y `effect()` para métricas de consistencia, latencia (P95/P99) y volumen.
- **Regla de Umbral 99.5%**: Colorimetría condicional (`status-success` vs `status-error`), badge visual y banner de alerta con llamada a la acción para triaje inmediato.
- **Micro-gráfica de Distribución**: Barra interactiva de transacciones con proporciones exactas (*Completadas*, *En Discrepancia*, *Fallidas*).

### 2. `TriageDrawerComponent`
- **Panel Lateral Deslizante (`mat-sidenav`)**: Acceso contextual a inconsistencias sin perder el estado del tablero.
- **Extracción de Logs en Tiempo Real**: Simula extracción de trazas de ejecución asociadas al ID de inconsistencia (`LOG-001`, `LOG-002`, `LOG-003`).
- **Visor de Diffs JSON**: Comparativa side-by-side entre valores esperados y obtenidos en transacciones bancarias.

### 3. `ExecutionFormComponent`
- **Generación Dinámica de Formularios (`DynamicFormGeneratorService`)**: Lee recursivamente `schema-v1.1.json` para construir `FormGroup` y `FormArray` reactivos.
- **Validación Integrada**: Botón para validar contrato contra el backend (`/validate-schema`) antes de encolar.
- **Disparo Asíncrono**: Despacha la suite al endpoint `/executions/start` e inicializa automáticamente la escucha de telemetría.

### 4. `TelemetryService`
- **Gestión de Conexión SignalR**: Conexión con reconexión automática (`withAutomaticReconnect`).
- **Estado con Signals**: `executionId`, `progressPercentage`, `currentStep`, `status`, `summaryToast`.
- **Modo Offline/Simulado**: Soporta fallback automático para demostración independiente de la UI.

---

---

## 🏢 Capa de Persistencia Multitenant (Nivel 1: Azure SQL + Global Query Filters)

El backend implementa un patrón de aislamiento de datos **Nivel 1 (Columna Discriminadora Compartida + Global Query Filters)** mediante **Entity Framework Core 8**:

1. **Resolución de Tenant (`ITenantService` / `TenantService`)**:
   - Resuelve el tenant activo desde `IHttpContextAccessor` con la siguiente prioridad:
     - Header HTTP: `X-Tenant-ID`
     - Query String: `?tenant=...`
     - Fallback: `"DEFAULT_TENANT"`
2. **Entidades con Contrato `ITenantEntity`**:
   - `ExecutionEntity`: Corridas E2E y estados de conciliación bancaria.
   - `InconsistencyEntity`: Registro detallado de discrepancias monetarias y valores JSON (Expected vs Actual).
   - `ScorecardMetricsEntity`: Consolidado de métricas de calidad y umbrales (99.5%).
3. **Global Query Filters Dinámicos (`AppDbContext`)**:
   - En `OnModelCreating`, registra automáticamente `HasQueryFilter(e => e.TenantId == CurrentTenantId)` en todas las entidades `ITenantEntity`. Ninguna consulta LINQ puede acceder a datos de otro tenant accidentalmente.
4. **Asignación Automática en Escrituras**:
   - `SaveChangesAsync` inyecta automáticamente el `TenantId` activo en cualquier entidad agregada (`EntityState.Added`).
5. **Azure SQL & Fallback In-Memory**:
   - Soporta **Azure SQL Database** mediante la cadena de conexión `ConnectionStrings:AzureSql` con reintentos exponenciales ante fallas transitorias (`EnableRetryOnFailure`). Si no se suministra cadena, conmuta a base de datos en memoria para pruebas ágiles.

---

## 🎭 Motor de Automatización Física UI (`UI_SEQUENCE` + Playwright)

El backend integra un motor físico de pruebas de interfaz de usuario con **Chromium Headless** para procesar secuencias `UI_SEQUENCE`:

1. **Comandos Soportados**:
   - `NAVIGATE`: Carga de URLs con verificación `DOMContentLoaded`.
   - `CLICK`: Interacción con botones y enlaces mediante selectores CSS / XPath.
   - `FILL`: Llenado de inputs y campos de formulario.
   - `SELECT_OPTION`: Selección de opciones en dropdowns.
   - `WAIT_FOR_SELECTOR`: Espera explícita de visibilidad de componentes.
   - `ASSERT_TEXT`: Verificación de valores y aserciones de negocio en el DOM.
2. **Políticas Estrictas de Captura de Evidencias**:
   - **Frecuencia (`ON_FAILURE_ONLY`)**: La captura se toma exclusivamente si ocurre un fallo.
   - **Compresión JPEG (75%)**: Límite estricto de tamaño $\le 250\text{ KB}$ por captura.
   - **Desacople de SignalR**: Se envía únicamente la bandera booleana `hasScreenshot: true` por WebSocket para no saturar el canal de telemetría.
   - **Persistencia & Carga Perezosa (Lazy Loading)**: La imagen Base64 se guarda en `InconsistencyEntity` y solo se entrega al frontend cuando el usuario abre el Triage Drawer vía `GET /api/v1/triage/logs/{inconsistencyId}`.

---

## 🗄️ Motor Físico de Ejecución SQL y Conciliación Masiva (`SQL_EXECUTE` & `DATA_COMPARE`)

El backend integra un conector físico de datos de alto rendimiento optimizado para procesar grandes volúmenes de registros bancarios (hasta **1,500,000 transacciones**) sin comprometer la memoria RAM ni saturar los WebSockets:

1. **Resolución Dinámica de Conexiones (`environmentRef`)**:
   - `SqlConnectionFactory` resuelve dinámicamente la cadena de conexión leyendo `ConnectionStrings:{environmentRef}_{Engine}` (ej. `ConnectionStrings:QA_Oracle`, `ConnectionStrings:DEV_SqlServer`, `ConnectionStrings:kv-bpp-ktx-qa_Oracle`).
   - Compatible nativamente con **Azure Container Apps (ACA)** inyectando secretos desde **Azure Key Vault**.
2. **Procesamiento por Lotes y Streaming de Memoria (`Chunk Size: 50,000`)**:
   - Lectura secuencial (`DbDataReader` con `CommandBehavior.SequentialAccess`) en lotes de 50,000 registros (~30 bloques para 1.5M de filas).
   - Huella de memoria $\le 150\text{ MB}$ con recolección periódica (`GC.Collect`).
   - `CommandTimeout` configurado en **300 segundos (5 minutos)** para tolerar consultas analíticas pesadas.
3. **Modo Fallback / Simulación Sintética (`MockDataGenerator`)**:
   - Generación sintética en streaming de 1.5M de registros cuando la base de datos física no está accesible o se activa `UseMockSqlData: true` en configuración.
4. **Motor de Aserciones y Calidad (`ASSERT_BUSINESS_RULES`)**:
   - Evaluación contra umbral de consistencia bancaria ($\ge 99.5\%$) y tolerancia monetaria (`numericTolerance`).
   - Cálculo automático de impacto financiero no conciliado y consolidación en `ScorecardMetricsEntity`.
   - Registro de discrepancias detalladas en `InconsistencyEntity` para triaje inmediato.

---

## 📑 Extracción Automática de Logs y Comparación de Archivos Excel/CSV

El backend incorpora soporte nativo para inspeccionar artefactos en el directorio `/out/`:

1. **Servicio de Extracción Automática de Logs (`FileLogExtractorService`)**:
   - Inspecciona el directorio de salida `/out/` aplicando una **estrategia de búsqueda híbrida**:
     - *Primaria:* Coincidencia por `executionId` (`{executionId}*.log`, `{executionId}*.txt`).
     - *Secundario/Fallback:* Archivo de traza más reciente (`LastWriteTimeUtc`) modificado dentro de la ventana de ejecución o por nombre de aplicativo.
   - Extrae el bloque contextual del error (últimas 25–30 líneas con excepciones o descalces).
   - Persiste el resultado en `InconsistencyEntity.ContextualLogs` para servirlo directamente al Triage Drawer vía `GET /api/v1/triage/logs/{inconsistencyId}`.

2. **Comparador Masivo de Archivos Excel y CSV (`ExcelAndCsvCompareExecutor`)**:
   - Lectura eficiente mediante `ExcelDataReader` (archivos `.xlsx` y `.xls` con soporte `CodePagesEncodingProvider`) y `CsvHelper` (archivos `.csv`).
   - **Autodetección de Delimitadores:** Identifica automáticamente comas (`,`), punto y coma (`;`) y tabuladores (`\t`).
   - **Manejo Robusto de Codificación:** Lectura en `UTF-8` con fallback automático a `ISO-8859-1` / `Windows-1252` para compatibilidad con caracteres en español y reportes bancarios legados.
   - Cruce por columnas clave (`KeyColumns`), cálculo de tolerancias numéricas y registro de discrepancias financieras en el Scorecard.

---

## 📡 Capa de Observabilidad y Telemetría Distribuida (OpenTelemetry & Azure Monitor)

El backend incorpora trazabilidad y métricas de observabilidad bajo el estándar **OpenTelemetry**:

1. **Trazado Distribuido (`ActivitySource: "MiTesterE2E.Orchestration"`)**:
   - Spans de orquestación para cada etapa: `ExecuteTestSuite`, `ProcessAction:UI_SEQUENCE`, `ProcessAction:DATA_COMPARE` y `ProcessAction:ASSERT_BUSINESS_RULES`.
   - Spans por cada lote de 50,000 registros (`ProcessBatchChunk`) con metadatos contextuales (`tenant.id`, `execution.id`, `records.processed`, `discrepancies.count`).
   - Muestreo al **100% (`AlwaysOnSampler`)** para certificación total bancaria (~30 spans por 1.5M registros).
2. **Métricas de Negocio y Rendimiento (`Meter: "MiTesterE2E.Metrics"`)**:
   - `mitester.transactions.processed_total` (`Counter<long>`): Total acumulado de transacciones conciliadas.
   - `mitester.execution.duration_ms` (`Histogram<double>`): Latencia de scripts SQL y comandos UI Playwright.
   - `mitester.inconsistencies.detected_total` (`Counter<long>`): Discrepancias detectadas categorizadas por tipo (`Monetary`, `Structural`, `UI`).
   - `mitester.reconciliation.consistency_pct` (`Histogram<double>`): Porcentaje de consistencia alcanzado.
3. **Exportación Dual Cloud-Ready (Azure Monitor / OTLP / Console)**:
   - Integración nativa con **Azure Application Insights** mediante `Azure.Monitor.OpenTelemetry.AspNetCore` activado automáticamente si `APPLICATIONINSIGHTS_CONNECTION_STRING` está presente en **Azure Container Apps (ACA)**.
   - Fallback automático a consola local y **OTLP Exporter** (`OTEL_EXPORTER_OTLP_ENDPOINT`) en entornos de desarrollo.

---

## 📋 Endpoints de la API Backend
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/orchestration/validate-schema` | `200 OK` | Valida un JSON de suite contra `schema-v1.1.json` devolviendo `isValid` y `validationErrors`. |
| `POST` | `/api/v1/orchestration/executions/start` | `202 Accepted` / `400 Bad Request` | Encola la ejecución de una suite (`UI_SEQUENCE`, SQL o masiva) y retorna el `executionId`. |
| `WS` | `/hubs/telemetry` | `101 Switching Protocols` | Conexión WebSocket para telemetría en tiempo real (`hasScreenshot: bool`). |
| `GET` | `/api/v1/triage/logs/{inconsistencyId}` | `200 OK` / `404 Not Found` | **Lazy Loading**: Retorna logs contextuales y captura de pantalla Base64 de la inconsistencia. |
| `GET` | `/api/v1/scorecarddata/metrics` | `200 OK` | Métricas del Scorecard filtradas automáticamente por el `TenantId` activo. |
| `GET` | `/api/v1/scorecarddata/inconsistencies` | `200 OK` | Lista de discrepancias e inconsistencias del tenant actual. |
| `GET` | `/api/v1/scorecarddata/executions` | `200 OK` | Historial de ejecuciones E2E del tenant actual. |
| `GET` | `/api/v1/scorecarddata/tenant-info` | `200 OK` | Diagnóstico del Tenant resuelto y su origen (Header/Query/Default). |

---

## 🔒 Auditoría de Seguridad y Buenas Prácticas

- **Cero Credenciales en Código**: No existen contraseñas, tokens o cadenas de conexión en el repositorio.
- **Aislamiento Multitenant Robusto**: Las consultas a base de datos están protegidas en el núcleo de EF Core por Global Filters.
- **Contenedor no privilegiado**: `Dockerfile` configurado con usuario `appuser` para **Azure Container Apps**.
- **Gestión de Secretos**: Variables de entorno e integración con **Azure Key Vault**.
- **Protección Git**: `.gitignore` auditado que excluye `node_modules`, binarios compilados, secretos y temporales.

---

## 🚀 Guía de Puesta en Marcha

### 1. Prerrequisitos
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js (v18+)](https://nodejs.org/) & `npm`
- [Docker](https://www.docker.com/) (opcional)

### 2. Ejecución Local en Desarrollo

**Backend (.NET 8):**
```bash
cd MiTesterE2E
dotnet run
```
Disponible en:
- **SPA Angular (si está compilada)**: `http://localhost:5000`
- **Swagger UI**: `http://localhost:5000/swagger`
- **SignalR Hub**: `http://localhost:5000/hubs/telemetry`

**Frontend (Angular 18+ Dev Server con Hot Reload):**
```bash
cd MiTesterE2E/ClientApp
npm install
npx ng serve --port 4200
```
Disponible en: `http://localhost:4200`.

### 3. Construcción del Contenedor Unificado (ACA)
```bash
# Construye tanto Angular como .NET en una única imagen multi-stage
docker build -t mitester-fullstack:latest -f MiTesterE2E/Dockerfile MiTesterE2E/

# Ejecutar el contenedor monolítico en puerto 8080
docker run -d -p 8080:8080 --name mitester-app mitester-fullstack:latest
```

---

## 📂 Estructura del Repositorio (Monolito Modular Unificado)

```text
├── .gitignore                     # Filtros de exclusión de artefactos y secretos
├── README.md                      # Documentación técnica principal
├── CONTEXTO_FASE1.md              # Requerimientos y arquitectura de negocio
├── schema-v1.1.json               # Contrato JSON Schema para suites E2E
├── swagger.yaml                   # Contrato OpenAPI 3.0 de endpoints REST
│
└── MiTesterE2E/                   # Monolito Modular (.NET 8 + SPA Angular)
    ├── MiTesterE2E.csproj         # Dependencias (.NET 8, NJsonSchema, SignalR)
    ├── Program.cs                 # Pipeline, Static Files SPA, CORS y SignalR
    ├── Dockerfile                 # Multi-stage Dockerfile (Node + .NET -> ACA)
    ├── Orchestration/             # Controladores, canales y worker en background
    ├── Telemetry/                 # Hub de SignalR y contratos de eventos
    ├── Persistence/               # EF Core 8, Multitenant DbContext y Entidades
    ├── Data/                      # Conector SQL masivo, streaming por lotes y reconciliación
    └── ClientApp/                 # Capa UI (Angular 18+ Standalone & M3)
        ├── package.json           # Dependencias (@angular/material, @microsoft/signalr)
        ├── src/
        │   ├── app/
        │   │   ├── app.config.ts  # Providers (HttpClient, AnimationsAsync, Routing)
        │   │   ├── app.component.ts # Shell (Navbar, Telemetry Bar)
        │   │   ├── core/          # TelemetryService, DynamicFormGeneratorService
        │   │   └── features/      # Dashboard, Triage Drawer, Execution Form
        │   └── styles/            # Tokens de diseño M3 y temas
```

---

## 👥 Equipo y Autoría
- **Proyecto**: Mi Tester E2E (AutoTest E2E)
- **Repositorio Oficial**: [https://github.com/jose9410/Mitester](https://github.com/jose9410/Mitester)

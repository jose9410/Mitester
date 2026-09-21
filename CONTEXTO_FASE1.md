# CONTEXTO TÉCNICO Y ARQUITECTURA - MI TESTER E2E (FASE 1)

## 1. Visión General del Proyecto y Negocio
"Mi Tester E2E" (AutoTest E2E) es una plataforma centralizada diseñada para entornos de certificación de calidad en plataformas bancarias críticas. Su objetivo fundamental es resolver la alta fricción cognitiva y los cuellos de botella operativos que enfrenta la analista de QA/requerimientos (arquetipo "Maria") debido a la dispersión de herramientas (Excel, logs descontextualizados, consolas de infraestructura, herramienta 'Concilia').

El sistema automatiza el seguimiento de ejecuciones asíncronas masivas (procesando hasta 1,500,000 transacciones) y habilita decisiones de liberación expeditas ("Go / No-Go") mediante un Dashboard con Scorecard de Conciliación (%) y un Módulo de Triage Asistido con etiquetado inteligente.

## 2. Decisiones de Arquitectura Técnica (Fase 1)
- **Backend:** Monolito Modular en .NET 8/9.
- **Despliegue:** Un único contenedor ejecutable alojado en **Azure Container Apps (ACA)**.
- **Frontend:** Angular (versión 18/más reciente) con Standalone Components, Material Design 3 (M3) y gestión de formularios mediante Dynamic Reactive Forms.
- **Arquitectura Basada en Metadatos (Metadata-Driven Engine):** Las suites de prueba E2E no están quemadas en código; se definen estrictamente mediante JSON y se validan contra el contrato `schema-v1.1.json` usando *NJsonSchema*.
- **Procesamiento Asíncrono en Memoria:** Desacoplamiento de peticiones HTTP de alta densidad mediante `System.Threading.Channels` (`Channel<ExecutionTask>`). No se utilizan brokers externos (RabbitMQ/Service Bus) para mantener el backend simplificado en un solo contenedor.
- **Telemetría en Tiempo Real:** Servidor WebSockets vía **SignalR** (`TelemetryHub`) para transmitir el progreso de ejecución (% procesado, paso activo, notificaciones Toast) hacia el frontend en Angular sin realizar *polling*.
- **Triage Asistido:** Extracción contextual de logs en Oracle/SQL Server y clasificación de fallos mediante etiquetado interno (`[Tag: Área Desarrollo]` / `[Tag: Área Soporte]`) sin integraciones externas a Azure DevOps/Jira en esta fase.

## 3. Estructura Interna del Monolito .NET

```
[API Controllers (Swagger)] ──> [Orchestrator Module] ──> [In-Memory Channel]
                                                                  │
[SignalR TelemetryHub] <─── [ExecutionBackgroundWorker] <────────┘
```

1. **`OrchestrationController`:** Expone endpoints REST (`/api/v1/orchestration/validate-schema` y `/api/v1/orchestration/executions/start`) para validar el JSON Schema v1.1 y encolar tareas asíncronas retornando `HTTP 202 Accepted`.
2. **`IExecutionTaskQueue` / `ExecutionTaskQueue`:** Encapsula el `Channel<ExecutionTask>` en memoria para paso de tareas seguro entre hilos.
3. **`ExecutionBackgroundWorker`:** `BackgroundService` que consume tareas del `Channel`, ejecuta la validación de lotes (1.5M de transacciones) de forma asíncrona y emite telemetría mediante `IHubContext<TelemetryHub>`.
4. **`TelemetryHub`:** Hub de SignalR que emite los eventos `ExecutionProgressUpdated` y `ExecutionCompletedToast`.
5. **`TriageController` & `TriageService`:** Consultas de registros de inconsistencia ordenados por impacto monetario (`Monetary_Value`), visor contextual de logs y asignación interna por área.
6. **`CertificationController` & `CertificationService`:** Métrica del Scorecard (porcentaje % vs umbral mínimo del 98.0%), cálculo del delta histórico ($v_{Actual}$ vs $v_{Anterior}$) y generación de certificados PDF.

## 4. Contratos de Referencia
- **`schema-v1.1.json`:** JSON Schema estricto con polimorfismo (`allOf`/`if-then`) para acciones (`UI_SEQUENCE`, `SQL_EXECUTE`, `ASSERT_BUSINESS_RULES`), manejo de tiempos de espera (`timeoutSeconds`), reintentos y metadatos visuales (`ui:widget`).
- **`swagger.yaml`:** Especificación OpenAPI 3.0 que define todos los DTOs de Request/Response y endpoints REST.

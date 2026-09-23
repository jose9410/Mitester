# 📋 Matriz de Control y Registro de Pruebas E2E — "Mi Tester E2E"

**Proyecto:** Mi Tester E2E — Plataforma de Certificación de Calidad Bancaria  
**Arquitectura:** .NET 8 Modular Monolith + Angular 19 SPA + Microsoft Playwright + OpenTelemetry  
**Fecha de Registro:** 2026-09-23  
**Estado General:** `[ ] EN PROCESO DE VALIDACIÓN LOCAL`

---

## 📊 Resumen de Control de Ejecución

| Módulo | Pruebas Totales | Exitosas (Pass) | Fallidas (Fail) | Pendientes |
| :--- | :---: | :---: | :---: | :---: |
| 1. Orquestación Metadatos & Dynamic Forms | 3 | 0 | 0 | 3 |
| 2. Persistencia & Multitenancy (EF Core 8) | 4 | 0 | 0 | 4 |
| 3. Motor Físico UI (Microsoft Playwright) | 3 | 0 | 0 | 3 |
| 4. Motor SQL & Archivos Masivos (Excel/CSV) | 6 | 0 | 0 | 6 |
| 5. Triage, Diagnóstico & Extracción de Logs | 4 | 0 | 0 | 4 |
| 6. Telemetría SignalR & Dashboard Scorecard | 4 | 0 | 0 | 4 |
| 7. Observabilidad Distribuida (OpenTelemetry) | 3 | 0 | 0 | 3 |
| **TOTAL** | **27** | **0** | **0** | **27** |

---

## 🧱 Módulo 1: Orquestación Metadatos & Formularios Dinámicos

> **Objetivo:** Validar la lectura del JSON Schema v1.1 y la generación de la interfaz reactiva sin código quemado.

- [ ] **TC-MOD1-01: Validación de JSON Schema v1.1**
  - **Descripción:** Confirmar que un archivo JSON fuera de especificación sea rechazado por el backend (.NET 8) notificando los errores de estructura.
  - **Resultado Esperado:** HTTP 400 Bad Request con detalle estructurado de validación contra `schema-v1.1.json`.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD1-02: Generación de Dynamic Reactive Forms**
  - **Descripción:** Verificar en Angular 19 que el esquema genere automáticamente los controles para editar `uiCommands`, `sqlCommands` y parámetros de reintento.
  - **Resultado Esperado:** Formulario reactivo renderizado dinámicamente según las definiciones del schema.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD1-03: Disparo Asíncrono de Ejecución**
  - **Descripción:** Validar que al enviar el proceso (`POST /api/v1/orchestration/executions/start`) el backend responda de inmediato con `HTTP 202 Accepted` y retorne el `executionId`.
  - **Resultado Esperado:** Respuesta asíncrona no bloqueante en < 200 ms con payload conteniendo `executionId`.
  - **Estado:** `[ ] Pending` | **Notas:** 

---

## 🔒 Módulo 2: Capa de Persistencia y Aislamiento Multitenant (Nivel 1)

> **Objetivo:** Garantizar el aislamiento estricto de datos entre entidades financieras sin riesgo de fuga de información.

- [ ] **TC-MOD2-01: Resolución de Tenant por Header HTTP**
  - **Descripción:** Consultar los endpoints enviando `X-Tenant-ID: BANCO_NACIONAL` y verificar que solo retorne datos de esa entidad.
  - **Resultado Esperado:** Métricas y ejecuciones exclusivas del tenant solicitado (ej. 99.12% consistencia, 2 inconsistencias).
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD2-02: Resolución por Query Param & Fallback**
  - **Descripción:** Probar el parámetro `?tenant=BANCO_REGIONAL` y verificar que a falta de encabezados conmute a `DEFAULT_TENANT`.
  - **Resultado Esperado:** Aislamiento correcto según parámetro o fallback transparente.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD2-03: Global Query Filters (EF Core 8)**
  - **Descripción:** Confirmar en la base de datos que ninguna consulta LINQ en `ScorecardDataController` o `AppDbContext` devuelva registros pertenecientes a otro `TenantId`.
  - **Resultado Esperado:** Filtro automático `HasQueryFilter(e => e.TenantId == CurrentTenantId)` aplicado en todas las consultas.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD2-04: Inyección Automática de TenantId en Escrituras**
  - **Descripción:** Crear registros en estado `EntityState.Added` y verificar que `SaveChangesAsync` inyecte el `TenantId` activo.
  - **Resultado Esperado:** Campo `TenantId` estampado automáticamente en base de datos sin requerir asignación manual.
  - **Estado:** `[ ] Pending` | **Notas:** 

---

## 🌐 Módulo 3: Motor Físico de Automatización UI (Microsoft Playwright)

> **Objetivo:** Auditar la navegación headless en Chromium sobre el aplicativo objetivo real.

- [ ] **TC-MOD3-01: Ejecución Secuencial de Comandos UI**
  - **Descripción:** Probar la secuencia de comandos (`NAVIGATE`, `FILL`, `CLICK`, `SELECT_OPTION`, `WAIT_FOR_SELECTOR`, `ASSERT_TEXT`).
  - **Resultado Esperado:** Interacción física exitosa sobre el DOM del aplicativo objetivo sin bloquear el worker.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD3-02: Captura de Evidencias ON_FAILURE_ONLY**
  - **Descripción:** Provocar un error deliberado en la interfaz y verificar que **únicamente ante fallos** se genere la captura de pantalla en formato JPEG comprimido (≤ 250 KB).
  - **Resultado Esperado:** Captura JPEG comprimida guardada en `InconsistencyEntity.ScreenshotBase64`, sin tomar capturas en ejecuciones exitosas.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD3-03: Resiliencia Local de Drivers Chromium**
  - **Descripción:** Comprobar que en desarrollo local el servicio descargue e inicialice los drivers de Chromium sin congelar la aplicación.
  - **Resultado Esperado:** Inicialización transparente mediante `PlaywrightCommandExecutor`.
  - **Estado:** `[ ] Pending` | **Notas:** 

---

## 🗄️ Módulo 4: Motor SQL, Archivos (Excel/CSV) y Conciliación Masiva

> **Objetivo:** Validar el procesamiento por lotes de grandes volúmenes transaccionales (1,500,000 registros).

- [ ] **TC-MOD4-01: Resolución Dinámica de Conexiones por environmentRef**
  - **Descripción:** Comprobar que el servicio resuelva correctamente las cadenas de conexión desde `ConnectionStrings:{environmentRef}_{Engine}`.
  - **Resultado Esperado:** Conexión exitosa a la base de datos de QA especificada en el JSON.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD4-02: Modo Fallback (MockDataGenerator)**
  - **Descripción:** Activar `UseMockSqlData: true` y verificar la generación sintética de 1.5M de registros para pruebas offline.
  - **Resultado Esperado:** Emisión progresiva de 30 lotes sintéticos por SignalR.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD4-03: Streaming de Memoria y Control de RAM**
  - **Descripción:** Procesar la conciliación de 1.5M de registros en lotes de 50,000 filas con `DbDataReader`.
  - **Resultado Esperado:** Procesamiento completo en ~30 lotes manteniendo la huella de RAM del backend < 150 MB.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD4-04: Autodetección de Delimitadores en CSV**
  - **Descripción:** Cargar reportes CSV con delimitadores coma (`,`), punto y coma (`;`) y tabulador (`\t`).
  - **Resultado Esperado:** Identificación y lectura correcta del delimitador sin errores de parseo.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD4-05: Soporte de Encoding Regional (ISO-8859-1 / Windows-1252)**
  - **Descripción:** Procesar archivos CSV/Excel guardados en codificación Latin1 y confirmar compatibilidad.
  - **Resultado Esperado:** Caracteres especiales (`ñ`, acentos, `$`) leídos correctamente sin corromper la memoria.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD4-06: Aserción de Reglas de Negocio (ASSERT_BUSINESS_RULES)**
  - **Descripción:** Evaluar el porcentaje global de conciliación contra el umbral configurado (ej. 99.5%).
  - **Resultado Esperado:** Cálculo exacto del % de consistencia e impacto financiero acumulado.
  - **Estado:** `[ ] Pending` | **Notas:** 

---

## 🩺 Módulo 5: Triage, Diagnóstico y Extracción de Logs

> **Objetivo:** Asegurar que la analista de QA pueda diagnosticar la causa raíz sin descargas manuales.

- [ ] **TC-MOD5-01: Extracción Automática desde el Directorio `/out/`**
  - **Descripción:** Verificar que `FileLogExtractorService` ubique el log de traza más reciente en la carpeta `/out/` y extraiga las últimas 25–30 líneas del error.
  - **Resultado Esperado:** Bloque contextual del log capturado y guardado en `InconsistencyEntity.ContextualLogs`.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD5-02: Visor Contextual (Drawer UI de Triage)**
  - **Descripción:** Hacer clic en una inconsistencia dentro de la SPA de Angular (`GET /api/v1/triage/logs/{inconsistencyId}`).
  - **Resultado Esperado:** Despliegue inmediato de las líneas de log en el panel lateral `mat-sidenav`.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD5-03: Carga Perezosa (Lazy Loading) de Imágenes**
  - **Descripción:** Confirmar que la captura JPEG en Base64 se descargue únicamente cuando el usuario abra el panel lateral de Triage.
  - **Resultado Esperado:** Flag `hasScreenshot: true` transmitido por WebSocket; payload Base64 descargado solo bajo demanda HTTP GET.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD5-04: Tagging Inteligente de Inconsistencias**
  - **Descripción:** Asignar y cambiar visualmente las etiquetas de fallo (*Desarrollo* vs. *Soporte*).
  - **Resultado Esperado:** Actualización inmediata en base de datos y reflejo visual en la tabla de Triage.
  - **Estado:** `[ ] Pending` | **Notas:** 

---

## 📊 Módulo 6: Telemetría SignalR & Dashboard Scorecard

> **Objetivo:** Monitorear el estado de las pruebas en tiempo real y la emisión del veredicto final.

- [ ] **TC-MOD6-01: Progreso en Tiempo Real por WebSockets**
  - **Descripción:** Disparar un proceso largo y verificar la actualización fluida de la barra de progreso en Angular 19.
  - **Resultado Esperado:** Eventos `ExecutionProgressUpdated` recibidos sin congelamiento de la UI.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD6-02: Protección del WebSocket de Sobrecarga**
  - **Descripción:** Inspeccionar las tramas de SignalR durante ejecuciones con falla.
  - **Resultado Esperado:** Transmisión exclusiva de la bandera booleana `hasScreenshot: true` (sin string Base64 pesado).
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD6-03: Emisión de Badge Go/No-Go**
  - **Descripción:** Verificar el comportamiento del Badge según el porcentaje de consistencia alcanzado.
  - **Resultado Esperado:** Consistencia ≥ 99.5% → Badge verde **PASS**; Consistencia < 99.5% → Badge rojo **FAIL**.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD6-04: Histórico de Tendencias y Deltas**
  - **Descripción:** Confirmar la representación visual de la evolución de calidad entre la ejecución actual y las previas.
  - **Resultado Esperado:** Gráfico de tendencias con deltas porcentuales (ej. `Delta -0.38%`).
  - **Estado:** `[ ] Pending` | **Notas:** 

---

## 📈 Módulo 7: Observabilidad Distribuida (OpenTelemetry)

> **Objetivo:** Auditar la salud del backend y la trazabilidad de operaciones.

- [ ] **TC-MOD7-01: Métricas Personalizadas de Negocio**
  - **Descripción:** Confirmar la emisión de contadores de transacciones (`mitester.transactions.processed_total`) e histogramas de consistencia (`mitester.reconciliation.consistency_pct`).
  - **Resultado Esperado:** Métricas expuestas correctamente en el pipeline de OpenTelemetry.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD7-02: Trazado Distribuido (Spans & ActivitySource)**
  - **Descripción:** Verificar la creación de Spans en `ExecutionBackgroundWorker`, `SqlCommandExecutor` (30 spans por cada 1.5M de filas) y Playwright.
  - **Resultado Esperado:** Trazabilidad completa correlacionada con `TraceId` y `SpanId` en logs.
  - **Estado:** `[ ] Pending` | **Notas:** 

- [ ] **TC-MOD7-03: Conmutación Dual de Exportador**
  - **Descripción:** Comprobar la conmutación entre entorno local y producción.
  - **Resultado Esperado:** Sin `APPLICATIONINSIGHTS_CONNECTION_STRING` → Output a Consola/OTLP; Con string → Exportación directa a Azure Monitor.
  - **Estado:** `[ ] Pending` | **Notas:** 

---

## 📝 Registro de Ejecución y Firmas de Aprobación

- **Ejecutado Por:** __________________________________
- **Fecha de Inicio:** ____ / ____ / ________
- **Fecha de Cierre:** ____ / ____ / ________
- **Veredicto Final de Certificación:** `[ ] APROBADO (GO)` | `[ ] RECHAZADO (NO-GO)`
- **Observaciones del Arquitecto:**  
  ____________________________________________________________________________________

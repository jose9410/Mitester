// ─────────────────────────────────────────────────────────────────────────────
// MODELOS DE TRIAGE Y SCORECARD
// Reflejan los DTOs del swagger.yaml del backend .NET 8
// ─────────────────────────────────────────────────────────────────────────────

/** Badge de estado de la decisión Go/No-Go */
export type StatusBadge = 'GO' | 'NO_GO' | 'REQUIRES_TRIAGE';

/** Destino de escalamiento de una inconsistencia */
export type TargetDestination = 'JIRA_DEVELOPMENT' | 'SUPPORT_QUEUE';

/** Etiqueta de área para clasificación de inconsistencias */
export type SuggestedTag = 'TAG_SOPORTE' | 'TAG_DESARROLLO';

/** Catálogo de base de datos afectada */
export type DbCatalog = 'Oracle' | 'SQL Server';

/** DTO del Scorecard (GET /certification/scorecard) */
export interface ScorecardResponse {
  tenant: string;
  processId: string;
  currentVersion: string;
  previousVersion: string;
  consistencyPercentage: number;
  acceptanceThreshold: number;
  deltaPreviousVersion: number;
  statusBadge: StatusBadge;
  totalTransactionsProcessed: number;
}

/** Item de inconsistencia en el módulo de Triage (GET /triage/inconsistencies) */
export interface InconsistencyItem {
  inconsistencyId: string;
  component: string;
  monetaryImpact: number;
  fieldAffected: string;
  suggestedTag: SuggestedTag;
}

/** Log contextual de una inconsistencia (GET /triage/logs/:id) */
export interface ContextualLogResponse {
  inconsistencyId: string;
  targetCatalog: DbCatalog;
  failedQuery: string;
  exactErrorMessage: string;
  suggestedActionText: string;
}

/** Request de escalamiento (POST /triage/escalate) */
export interface EscalateRequest {
  inconsistencyId: string;
  targetDestination: TargetDestination;
  customNotes?: string;
}

/** Response de escalamiento */
export interface EscalateResponse {
  ticketId: string;
  status: string;
}

/** Request para iniciar ejecución (POST /orchestration/executions/start) */
export interface StartExecutionRequest {
  suiteConfigJson: string;
}

/** Response de ejecución iniciada (HTTP 202) */
export interface StartExecutionResponse {
  executionId: string;
  status: 'PROCESSING_ASYNC';
}

/** Response de validación de schema */
export interface ValidationResponse {
  isValid: boolean;
  validationErrors: string[];
}

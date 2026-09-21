// ─────────────────────────────────────────────────────────────────────────────
// MODELOS DE TELEMETRÍA
// Reflejan exactamente los payloads que emite TelemetryHub en el backend .NET 8
// ─────────────────────────────────────────────────────────────────────────────

/** Payload del evento SignalR "ExecutionProgressUpdated" */
export interface ProgressUpdatedPayload {
  executionId: string;
  progressPercentage: number;          // 0 - 100
  activeStepDescription: string;
  timestamp: string;                   // ISO 8601 UTC
}

/** Payload del evento SignalR "ExecutionCompletedToast" */
export interface ExecutionCompletedPayload {
  executionId: string;
  finalStatus: 'COMPLETED_SUCCESS' | 'COMPLETED_WITH_ERRORS' | 'FAILED';
  message: string;
  durationSeconds: number;
  completedAt: string;                 // ISO 8601 UTC
}

/** Estado de la conexión WebSocket con el TelemetryHub */
export type ConnectionStatus = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting' | 'Error';

/** Snapshot del estado de telemetría expuesto como Signals */
export interface TelemetryState {
  connectionStatus: ConnectionStatus;
  executionId: string | null;
  progressPercentage: number;
  activeStepDescription: string;
  latestToast: ExecutionCompletedPayload | null;
  isRunning: boolean;
}

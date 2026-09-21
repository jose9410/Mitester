import { Injectable, OnDestroy, signal, computed, inject } from '@angular/core';
import { Subject } from 'rxjs';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import {
  ConnectionStatus,
  ExecutionCompletedPayload,
  ProgressUpdatedPayload,
} from '../models/telemetry.models';
import { environment } from '../../../environments/environment';

// ─────────────────────────────────────────────────────────────────────────────
// TelemetryService
// ─────────────────────────────────────────────────────────────────────────────
// Servicio Singleton que gestiona el ciclo de vida completo de la conexión
// WebSocket con el TelemetryHub del backend .NET 8.
//
// Estrategia de reactividad:
//   - Internamente: RxJS Subject para los eventos brutos de SignalR
//   - Expuesto a componentes: Angular Signals (signal / computed)
//
// Flujo:
//   SignalR Hub → Subject → tap/next → writable Signal → computed Signals en componentes
// ─────────────────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class TelemetryService implements OnDestroy {

  // ── SUBJECTS INTERNOS (para interoperabilidad con RxJS si se requiere) ─────
  private readonly progressSubject$ = new Subject<ProgressUpdatedPayload>();
  private readonly completedSubject$ = new Subject<ExecutionCompletedPayload>();

  // Expuesto como Observable para casos de uso avanzado (pipe, combineLatest, etc.)
  readonly progress$  = this.progressSubject$.asObservable();
  readonly completed$ = this.completedSubject$.asObservable();

  // ── SIGNALS ESCRITOS (private) ────────────────────────────────────────────
  private readonly _connectionStatus  = signal<ConnectionStatus>('Disconnected');
  private readonly _executionId       = signal<string | null>(null);
  private readonly _progressPct       = signal<number>(0);
  private readonly _activeStep        = signal<string>('');
  private readonly _latestToast       = signal<ExecutionCompletedPayload | null>(null);
  private readonly _isRunning         = signal<boolean>(false);

  // ── SIGNALS PÚBLICOS (read-only, expuestos a componentes) ─────────────────
  readonly connectionStatus  = this._connectionStatus.asReadonly();
  readonly executionId       = this._executionId.asReadonly();
  readonly progressPct       = this._progressPct.asReadonly();
  readonly activeStep        = this._activeStep.asReadonly();
  readonly latestToast       = this._latestToast.asReadonly();
  readonly isRunning         = this._isRunning.asReadonly();

  // ── SIGNALS COMPUTADOS (derivados, sin costo extra) ──────────────────────
  /** true si la conexión WebSocket está operativa */
  readonly isConnected = computed(() => this._connectionStatus() === 'Connected');
  /** Porcentaje como string formateado: "87%" */
  readonly progressLabel = computed(() => `${this._progressPct()}%`);
  /** Valor normalizado 0.0-1.0 para mat-progress-bar mode="determinate" */
  readonly progressValue = computed(() => this._progressPct());

  // ── CONEXIÓN SIGNALR ──────────────────────────────────────────────────────
  private connection!: HubConnection;
  private reconnectAttempts = 0;

  constructor() {
    this.buildConnection();
  }

  // ── MÉTODOS PÚBLICOS ──────────────────────────────────────────────────────

  /**
   * Inicia la conexión WebSocket hacia el TelemetryHub.
   * Debe llamarse al inicializar el AppComponent o la feature de Dashboard.
   */
  async connect(): Promise<void> {
    if (this.connection.state !== HubConnectionState.Disconnected) return;

    this._connectionStatus.set('Connecting');
    try {
      await this.connection.start();
      this._connectionStatus.set('Connected');
      this.reconnectAttempts = 0;
      console.info('[TelemetryService] Conectado al TelemetryHub en', environment.hubUrl);
    } catch (err) {
      this._connectionStatus.set('Error');
      console.error('[TelemetryService] Error al conectar:', err);
    }
  }

  /**
   * Detiene la conexión WebSocket de forma graciosa.
   */
  async disconnect(): Promise<void> {
    if (this.connection.state !== HubConnectionState.Disconnected) {
      await this.connection.stop();
      this._connectionStatus.set('Disconnected');
    }
  }

  /**
   * Asocia un executionId a la sesión de telemetría activa.
   * Los componentes pueden filtrar eventos por este valor.
   */
  trackExecution(executionId: string): void {
    this._executionId.set(executionId);
    this._progressPct.set(0);
    this._activeStep.set('Inicializando...');
    this._isRunning.set(true);
    this._latestToast.set(null);
  }

  // ── CONSTRUCCIÓN DE LA CONEXIÓN ───────────────────────────────────────────

  private buildConnection(): void {
    this.connection = new HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      // Reconexión automática con backoff exponencial: 0s, 2s, 10s, 30s
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    // ── Registro de handlers de eventos SignalR ───────────────────────────
    this.connection.on(
      'ExecutionProgressUpdated',
      (payload: ProgressUpdatedPayload) => this.onProgressUpdated(payload),
    );

    this.connection.on(
      'ExecutionCompletedToast',
      (payload: ExecutionCompletedPayload) => this.onExecutionCompleted(payload),
    );

    // ── Handlers de estado de conexión ───────────────────────────────────
    this.connection.onreconnecting(() => {
      this._connectionStatus.set('Reconnecting');
      console.warn('[TelemetryService] Reconectando...');
    });

    this.connection.onreconnected(() => {
      this._connectionStatus.set('Connected');
      console.info('[TelemetryService] Reconectado exitosamente.');
    });

    this.connection.onclose((err) => {
      this._connectionStatus.set(err ? 'Error' : 'Disconnected');
      if (err) console.error('[TelemetryService] Conexión cerrada con error:', err);
    });
  }

  // ── HANDLERS DE EVENTOS ───────────────────────────────────────────────────

  private onProgressUpdated(payload: ProgressUpdatedPayload): void {
    // Solo procesar si el executionId coincide (o si no hay filtro activo)
    const tracked = this._executionId();
    if (tracked && tracked !== payload.executionId) return;

    // Actualizar Signals — Angular detectará el cambio en la próxima microtask
    this._progressPct.set(payload.progressPercentage);
    this._activeStep.set(payload.activeStepDescription);

    // También emitir al Subject para observadores RxJS avanzados
    this.progressSubject$.next(payload);
  }

  private onExecutionCompleted(payload: ExecutionCompletedPayload): void {
    const tracked = this._executionId();
    if (tracked && tracked !== payload.executionId) return;

    this._progressPct.set(100);
    this._isRunning.set(false);
    this._latestToast.set(payload);

    this.completedSubject$.next(payload);
  }

  // ── LIFECYCLE ─────────────────────────────────────────────────────────────
  ngOnDestroy(): void {
    this.disconnect();
    this.progressSubject$.complete();
    this.completedSubject$.complete();
  }
}

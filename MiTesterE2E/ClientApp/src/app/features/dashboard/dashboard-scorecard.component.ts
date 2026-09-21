import {
  Component, OnInit, OnDestroy, inject, signal, computed, effect,
} from '@angular/core';
import { CommonModule }           from '@angular/common';
import { MatCardModule }          from '@angular/material/card';
import { MatProgressBarModule }   from '@angular/material/progress-bar';
import { MatIconModule }          from '@angular/material/icon';
import { MatButtonModule }        from '@angular/material/button';
import { MatChipsModule }         from '@angular/material/chips';
import { MatTooltipModule }       from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { TelemetryService }  from '../../core/services/telemetry.service';
import { ScorecardResponse, StatusBadge } from '../../core/models/triage.models';

// ─────────────────────────────────────────────────────────────────────────────
// DashboardScorecardComponent
// ─────────────────────────────────────────────────────────────────────────────
// Tablero de control de certificación bancaria.
//
// Muestra:
//   - Porcentaje de consistencia actual vs umbral mínimo 98.0%
//   - Badge de estado Go / No-Go / Requires Triage (con ícono WCAG)
//   - Delta histórico respecto a la versión anterior
//   - Barra de progreso asíncrona en tiempo real (vía TelemetryService Signals)
//   - Toast de notificación cuando la ejecución finaliza
// ─────────────────────────────────────────────────────────────────────────────

@Component({
  selector: 'app-dashboard-scorecard',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatProgressBarModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatTooltipModule,
    MatSnackBarModule,
  ],
  templateUrl: './dashboard-scorecard.component.html',
  styleUrls:   ['./dashboard-scorecard.component.scss'],
})
export class DashboardScorecardComponent implements OnInit, OnDestroy {

  // ── DEPENDENCIAS ──────────────────────────────────────────────────────────
  private readonly telemetry = inject(TelemetryService);
  private readonly snackBar  = inject(MatSnackBar);

  // ── SIGNALS DEL COMPONENTE ────────────────────────────────────────────────
  readonly scorecard = signal<ScorecardResponse | null>(null);
  readonly isLoading = signal<boolean>(true);
  readonly error     = signal<string | null>(null);

  // ── SIGNALS DEL SERVICIO DE TELEMETRÍA (read-only, reactivos) ────────────
  readonly connectionStatus  = this.telemetry.connectionStatus;
  readonly progressValue     = this.telemetry.progressValue;
  readonly progressLabel     = this.telemetry.progressLabel;
  readonly activeStep        = this.telemetry.activeStep;
  readonly isRunning         = this.telemetry.isRunning;
  readonly isConnected       = this.telemetry.isConnected;
  readonly executionId       = this.telemetry.executionId;

  // ── COMPUTADOS DEL SCORECARD ──────────────────────────────────────────────
  readonly consistencyPct = computed(() => this.scorecard()?.consistencyPercentage ?? 0);
  readonly threshold      = computed(() => this.scorecard()?.acceptanceThreshold   ?? 98.0);
  readonly delta          = computed(() => this.scorecard()?.deltaPreviousVersion   ?? 0);
  readonly statusBadge    = computed(() => this.scorecard()?.statusBadge            ?? 'REQUIRES_TRIAGE');
  readonly isAboveThreshold = computed(() => this.consistencyPct() >= this.threshold());

  readonly badgeIcon = computed<string>(() => {
    const badge = this.statusBadge();
    if (badge === 'GO')             return 'check_circle';
    if (badge === 'NO_GO')          return 'error';
    return 'warning';
  });

  readonly badgeClass = computed<string>(() => {
    const badge = this.statusBadge();
    if (badge === 'GO')    return 'badge-go';
    if (badge === 'NO_GO') return 'badge-nogo';
    return 'badge-triage';
  });

  readonly deltaLabel = computed<string>(() => {
    const d = this.delta();
    return d >= 0 ? `+${d.toFixed(2)}%` : `${d.toFixed(2)}%`;
  });

  readonly deltaClass = computed<string>(() =>
    this.delta() >= 0 ? 'delta-positive' : 'delta-negative',
  );

  readonly totalTxFormatted = computed<string>(() => {
    const n = this.scorecard()?.totalTransactionsProcessed ?? 0;
    return new Intl.NumberFormat('es-CO').format(n);
  });

  // ── EFFECT: REACCIÓN AL TOAST DE SIGNALR ─────────────────────────────────
  private readonly toastEffect = effect(() => {
    const toast = this.telemetry.latestToast();
    if (!toast) return;

    const isSuccess = toast.finalStatus === 'COMPLETED_SUCCESS';
    const panelClass = isSuccess ? 'snack-success' : 'snack-error';
    const icon = isSuccess ? '✅' : '❌';

    this.snackBar.open(
      `${icon} ${toast.message} (${toast.durationSeconds.toFixed(1)}s)`,
      'Cerrar',
      { duration: 8000, panelClass, horizontalPosition: 'right', verticalPosition: 'top' },
    );

    // Recargar el scorecard simulado tras la finalización
    if (isSuccess) this.loadScorecard();
  });

  // ── LIFECYCLE ─────────────────────────────────────────────────────────────

  async ngOnInit(): Promise<void> {
    await this.telemetry.connect();
    this.loadScorecard();
  }

  ngOnDestroy(): void {
    // TelemetryService es Singleton — no se desconecta aquí,
    // sólo el AppComponent debe desconectar al salir
  }

  // ── MÉTODOS PÚBLICOS ──────────────────────────────────────────────────────

  /** Simula la carga del scorecard (en Fase 2 se conectará al endpoint REST) */
  loadScorecard(): void {
    this.isLoading.set(true);
    this.error.set(null);

    // Simulación de datos de respuesta del backend
    setTimeout(() => {
      this.scorecard.set({
        tenant: 'BPP_KTX_SAAS',
        processId: 'Cruce_Tx_Breb',
        currentVersion: 'v2.4.1',
        previousVersion: 'v2.4.0',
        consistencyPercentage: 98.5,
        acceptanceThreshold: 98.0,
        deltaPreviousVersion: 0.3,
        statusBadge: 'GO',
        totalTransactionsProcessed: 1_500_000,
      });
      this.isLoading.set(false);
    }, 800);
  }

  /** Mapa de etiqueta de badge a texto legible en español */
  getBadgeLabel(badge: StatusBadge): string {
    return { GO: '✓ GO', NO_GO: '✗ NO-GO', REQUIRES_TRIAGE: '⚠ REQUIRES TRIAGE' }[badge];
  }
}

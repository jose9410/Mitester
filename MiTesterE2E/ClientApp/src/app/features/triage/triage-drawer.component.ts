import {
  Component, OnInit, inject, signal, computed,
} from '@angular/core';
import { CommonModule }        from '@angular/common';
import { FormsModule }         from '@angular/forms';
import { MatSidenavModule }    from '@angular/material/sidenav';
import { MatCardModule }       from '@angular/material/card';
import { MatListModule }       from '@angular/material/list';
import { MatIconModule }       from '@angular/material/icon';
import { MatButtonModule }     from '@angular/material/button';
import { MatChipsModule }      from '@angular/material/chips';
import { MatDividerModule }    from '@angular/material/divider';
import { MatFormFieldModule }  from '@angular/material/form-field';
import { MatInputModule }      from '@angular/material/input';
import { MatBadgeModule }      from '@angular/material/badge';
import { MatTooltipModule }    from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar }         from '@angular/material/snack-bar';

import {
  InconsistencyItem, ContextualLogResponse,
  EscalateRequest, EscalateResponse,
} from '../../core/models/triage.models';

// ─────────────────────────────────────────────────────────────────────────────
// TriageDrawerComponent
// ─────────────────────────────────────────────────────────────────────────────
// Panel lateral de diagnóstico y triage asistido.
//
// Funcionalidades:
//   - Lista de inconsistencias ordenada por impacto monetario
//   - Drawer lateral con log contextual de Oracle/SQL Server
//   - Etiquetado rápido: [Tag: Área Desarrollo] o [Tag: Área Soporte]
//   - Escalamiento simulado a JIRA_DEVELOPMENT o SUPPORT_QUEUE
// ─────────────────────────────────────────────────────────────────────────────

@Component({
  selector: 'app-triage-drawer',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatSidenavModule,
    MatCardModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatDividerModule,
    MatFormFieldModule,
    MatInputModule,
    MatBadgeModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './triage-drawer.component.html',
  styleUrls:   ['./triage-drawer.component.scss'],
})
export class TriageDrawerComponent implements OnInit {

  private readonly snackBar = inject(MatSnackBar);

  // ── SIGNALS DE ESTADO ─────────────────────────────────────────────────────
  readonly inconsistencies  = signal<InconsistencyItem[]>([]);
  readonly selectedItem     = signal<InconsistencyItem | null>(null);
  readonly contextualLog    = signal<ContextualLogResponse | null>(null);
  readonly isDrawerOpen     = signal<boolean>(false);
  readonly isLoadingList    = signal<boolean>(true);
  readonly isLoadingLog     = signal<boolean>(false);
  readonly isEscalating     = signal<boolean>(false);
  readonly customNotes      = signal<string>('');
  readonly executionId      = signal<string>('a1b2c3d4-e5f6-7890-abcd-ef1234567890');

  // ── COMPUTADOS ────────────────────────────────────────────────────────────
  readonly totalCount = computed(() => this.inconsistencies().length);
  readonly totalImpact = computed(() =>
    this.inconsistencies().reduce((sum, i) => sum + i.monetaryImpact, 0),
  );
  readonly totalImpactFormatted = computed(() =>
    new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })
      .format(this.totalImpact()),
  );

  // ── LIFECYCLE ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.loadInconsistencies();
  }

  // ── CARGA DE DATOS (simulada — Fase 2 conectará al endpoint REST) ─────────
  loadInconsistencies(): void {
    this.isLoadingList.set(true);
    setTimeout(() => {
      this.inconsistencies.set([
        { inconsistencyId: 'INV-8092', component: 'Módulo ATH / Préstamos',
          monetaryImpact: 450_000, fieldAffected: 'Monetary_Value', suggestedTag: 'TAG_SOPORTE' },
        { inconsistencyId: 'INV-7841', component: 'Cruce BREB / Cuentas',
          monetaryImpact: 312_500, fieldAffected: 'Balance_Final', suggestedTag: 'TAG_DESARROLLO' },
        { inconsistencyId: 'INV-9103', component: 'GouPayments / Débitos',
          monetaryImpact: 98_750, fieldAffected: 'Tx_Amount', suggestedTag: 'TAG_SOPORTE' },
        { inconsistencyId: 'INV-6522', component: 'Koncilia / Créditos',
          monetaryImpact: 55_200, fieldAffected: 'Credit_Value', suggestedTag: 'TAG_DESARROLLO' },
      ]);
      this.isLoadingList.set(false);
    }, 700);
  }

  openDrawer(item: InconsistencyItem): void {
    this.selectedItem.set(item);
    this.customNotes.set('');
    this.contextualLog.set(null);
    this.isDrawerOpen.set(true);
    this.loadContextualLog(item.inconsistencyId);
  }

  closeDrawer(): void {
    this.isDrawerOpen.set(false);
    setTimeout(() => { this.selectedItem.set(null); this.contextualLog.set(null); }, 300);
  }

  private loadContextualLog(inconsistencyId: string): void {
    this.isLoadingLog.set(true);
    setTimeout(() => {
      // Datos simulados — en Fase 2 llama a GET /triage/logs/:id
      this.contextualLog.set({
        inconsistencyId,
        targetCatalog: 'Oracle',
        failedQuery: `SELECT Monetary_Value FROM DB_ORACLE_LOANS\nWHERE Tx_Id = '${inconsistencyId.replace('INV-', '')}'\n  AND Processing_Date = TRUNC(SYSDATE)`,
        exactErrorMessage: `ORA-01403: Campo Monetary_Value nulo en tabla DB_ORACLE_LOANS para Tx_Id ${inconsistencyId}.`,
        suggestedActionText: 'Inyección incompleta de datos de prueba en esquema Oracle STAGING. Verificar pipeline de carga inicial.',
      });
      this.isLoadingLog.set(false);
    }, 500);
  }

  // ── ESCALAMIENTO ──────────────────────────────────────────────────────────
  escalate(targetDestination: 'JIRA_DEVELOPMENT' | 'SUPPORT_QUEUE'): void {
    const item = this.selectedItem();
    if (!item) return;

    this.isEscalating.set(true);

    const req: EscalateRequest = {
      inconsistencyId: item.inconsistencyId,
      targetDestination,
      customNotes: this.customNotes() || undefined,
    };

    // Simulación — en Fase 2 llama a POST /triage/escalate
    setTimeout(() => {
      const mockResponse: EscalateResponse = {
        ticketId: targetDestination === 'JIRA_DEVELOPMENT'
          ? `JIRA-E2E-${Math.floor(Math.random() * 900 + 100)}`
          : `SUP-${Math.floor(Math.random() * 9000 + 1000)}`,
        status: 'ESCALATED_SUCCESSFULLY',
      };

      this.isEscalating.set(false);
      this.snackBar.open(
        `✅ Escalado: Ticket ${mockResponse.ticketId} creado exitosamente`,
        'Cerrar',
        { duration: 6000, panelClass: 'snack-success', horizontalPosition: 'right', verticalPosition: 'top' },
      );
      this.closeDrawer();
    }, 1200);
  }

  // ── UTILIDADES ────────────────────────────────────────────────────────────
  formatImpact(value: number): string {
    return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })
      .format(value);
  }

  getTagLabel(tag: string): string {
    return tag === 'TAG_DESARROLLO' ? 'Área Desarrollo' : 'Área Soporte';
  }

  getTagClass(tag: string): string {
    return tag === 'TAG_DESARROLLO' ? 'tag-desarrollo' : 'tag-soporte';
  }

  getTagIcon(tag: string): string {
    return tag === 'TAG_DESARROLLO' ? 'code' : 'support_agent';
  }
}

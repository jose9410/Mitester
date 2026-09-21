import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule }  from '@angular/material/button';
import { MatIconModule }    from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule }    from '@angular/material/list';
import { MatSnackBarModule } from '@angular/material/snack-bar';

import { TelemetryService } from './core/services/telemetry.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatSidenavModule,
    MatListModule,
    MatSnackBarModule,
  ],
  template: `
    <mat-sidenav-container class="app-container">

      <!-- Sidebar de Navegación -->
      <mat-sidenav #sidenav mode="side" opened class="app-sidenav">
        <!-- Logo / Brand -->
        <div class="nav-brand">
          <mat-icon class="brand-icon">verified_user</mat-icon>
          <div class="brand-text">
            <span class="brand-name">Mi Tester E2E</span>
            <span class="brand-version">v1.1.0 — Fase 1</span>
          </div>
        </div>

        <!-- Items de Navegación -->
        <mat-nav-list class="nav-list">
          <a mat-list-item routerLink="/dashboard" routerLinkActive="nav-active"
             [routerLinkActiveOptions]="{ exact: false }"
             matTooltip="Dashboard de Certificación" matTooltipPosition="right">
            <mat-icon matListItemIcon>dashboard</mat-icon>
            <span matListItemTitle>Dashboard</span>
          </a>
          <a mat-list-item routerLink="/triage" routerLinkActive="nav-active"
             matTooltip="Módulo de Triage" matTooltipPosition="right">
            <mat-icon matListItemIcon>bug_report</mat-icon>
            <span matListItemTitle>Triage</span>
          </a>
          <a mat-list-item routerLink="/execution" routerLinkActive="nav-active"
             matTooltip="Configurar y lanzar ejecución" matTooltipPosition="right">
            <mat-icon matListItemIcon>play_circle</mat-icon>
            <span matListItemTitle>Ejecución</span>
          </a>
        </mat-nav-list>

        <!-- Estado SignalR en el footer del nav -->
        <div class="nav-footer">
          <div class="signalr-status" [class]="'status-' + telemetry.connectionStatus()">
            <span class="status-dot"></span>
            <span class="status-text">{{ telemetry.connectionStatus() }}</span>
          </div>
        </div>
      </mat-sidenav>

      <!-- Contenido Principal -->
      <mat-sidenav-content class="app-content">
        <!-- Barra Superior -->
        <mat-toolbar class="app-toolbar">
          <span class="toolbar-title">Plataforma de Certificación Bancaria</span>
          <span class="spacer"></span>
          <span class="tenant-info">BPP_KTX_SAAS</span>
        </mat-toolbar>

        <!-- Barra de Progreso Global (visible cuando hay ejecución activa) -->
        @if (telemetry.isRunning()) {
          <div class="global-progress">
            <div
              class="progress-fill"
              [style.width.%]="telemetry.progressValue()"
            ></div>
          </div>
        }

        <!-- Router Outlet -->
        <main class="main-content">
          <router-outlet></router-outlet>
        </main>
      </mat-sidenav-content>

    </mat-sidenav-container>
  `,
  styles: [`
    @use '../styles/tokens' as t;

    .app-container { height: 100vh; }

    // Sidebar
    .app-sidenav {
      width: 220px;
      background: t.$primary;
      border: none !important;
      display: flex;
      flex-direction: column;
    }

    .nav-brand {
      display: flex;
      align-items: center;
      gap: t.$space-3;
      padding: t.$space-4 t.$space-4 t.$space-6;
      border-bottom: 1px solid rgba(white, .1);

      .brand-icon { color: white; font-size: 2rem; }

      .brand-text {
        display: flex;
        flex-direction: column;

        .brand-name {
          color: white;
          font-weight: 700;
          font-size: .95rem;
          line-height: 1.2;
        }

        .brand-version {
          color: rgba(white, .5);
          font-size: .65rem;
          font-family: t.$font-mono;
        }
      }
    }

    .nav-list {
      padding: t.$space-3 0;
      flex: 1;

      a {
        color: rgba(white, .75) !important;
        margin: 2px t.$space-2;
        border-radius: t.$radius-sm !important;
        transition: all t.$transition-fast;

        mat-icon { color: rgba(white, .6) !important; }

        &:hover {
          background: rgba(white, .1) !important;
          color: white !important;
          mat-icon { color: white !important; }
        }

        &.nav-active {
          background: rgba(white, .15) !important;
          color: white !important;
          mat-icon { color: white !important; }
          border-left: 3px solid white;
        }
      }
    }

    // Status SignalR en footer del nav
    .nav-footer {
      padding: t.$space-4;
      border-top: 1px solid rgba(white, .1);

      .signalr-status {
        display: flex;
        align-items: center;
        gap: t.$space-2;
        font-size: .7rem;
        color: rgba(white, .6);

        .status-dot {
          width: 6px; height: 6px;
          border-radius: 50%;
          background: rgba(white, .4);
        }

        &.status-Connected {
          color: #81C784;
          .status-dot { background: #81C784; }
        }
        &.status-Reconnecting {
          .status-dot { background: #FFB74D; animation: pulse 1s infinite; }
        }
      }
    }

    // Toolbar
    .app-toolbar {
      background: t.$surface-card !important;
      color: t.$text-primary !important;
      border-bottom: 1px solid t.$surface-border !important;
      box-shadow: t.$shadow-sm !important;
      height: 56px;

      .toolbar-title { font-weight: 600; font-size: .9rem; }
      .spacer { flex: 1; }
      .tenant-info {
        font-size: .75rem;
        font-family: t.$font-mono;
        background: t.$surface-bg;
        padding: 4px 10px;
        border-radius: 99px;
        border: 1px solid t.$surface-border;
        color: t.$text-secondary;
      }
    }

    // Barra de progreso global (top del contenido)
    .global-progress {
      height: 3px;
      background: t.$surface-border;
      overflow: hidden;

      .progress-fill {
        height: 100%;
        background: linear-gradient(90deg, t.$primary, t.$go-color);
        transition: width 0.4s ease;
      }
    }

    .app-content {
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }

    .main-content {
      flex: 1;
      overflow-y: auto;
      background-color: t.$surface-bg;
    }
  `],
})
export class AppComponent implements OnInit {
  readonly telemetry = inject(TelemetryService);

  async ngOnInit(): Promise<void> {
    // Conectar SignalR al arrancar la aplicación
    await this.telemetry.connect();
  }
}

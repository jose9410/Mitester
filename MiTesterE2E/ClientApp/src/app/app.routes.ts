import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./features/dashboard/dashboard-scorecard.component')
        .then((m) => m.DashboardScorecardComponent),
  },
  {
    path: 'triage',
    loadComponent: () =>
      import('./features/triage/triage-drawer.component')
        .then((m) => m.TriageDrawerComponent),
  },
  {
    path: 'execution',
    loadComponent: () =>
      import('./features/execution-form/execution-form.component')
        .then((m) => m.ExecutionFormComponent),
  },
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: '**', redirectTo: 'dashboard' },
];

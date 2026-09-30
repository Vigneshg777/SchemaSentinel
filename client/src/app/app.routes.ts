import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/analyzer/analyzer').then((m) => m.Analyzer),
    title: 'SchemaSentinel — Analyzer',
  },
  {
    path: 'history',
    loadComponent: () => import('./features/history/history').then((m) => m.History),
    title: 'SchemaSentinel — History',
  },
  {
    path: 'history/:id',
    loadComponent: () =>
      import('./features/history/history-detail').then((m) => m.HistoryDetail),
    title: 'SchemaSentinel — Analysis',
  },
  { path: '**', redirectTo: '' },
];

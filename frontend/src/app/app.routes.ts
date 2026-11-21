import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'Comprobante: generador y validador de CFDI 4.0',
    loadComponent: () => import('./features/inicio/inicio').then((m) => m.Inicio),
  },
  { path: '**', redirectTo: '' },
];

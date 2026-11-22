import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'Comprobante: generador y validador de CFDI 4.0',
    loadComponent: () => import('./features/inicio/inicio').then((m) => m.Inicio),
  },
  {
    path: 'generar',
    title: 'Generar un CFDI · Comprobante',
    loadComponent: () => import('./features/generar/generar').then((m) => m.Generar),
  },
  {
    path: 'validar',
    title: 'Validar un XML · Comprobante',
    loadComponent: () => import('./features/validar/validar').then((m) => m.Validar),
  },
  {
    path: 'catalogos',
    title: 'Catálogos del SAT · Comprobante',
    loadComponent: () => import('./features/catalogos/catalogos').then((m) => m.Catalogos),
  },
  { path: '**', redirectTo: '' },
];

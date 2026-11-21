import { provideHttpClient, withFetch } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS, MatFormFieldDefaultOptions } from '@angular/material/form-field';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { routes } from './app.routes';
import { provideIdioma } from './core/idioma';
import { registrarIconos } from './core/iconos';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch()),
    provideRouter(routes, withComponentInputBinding()),
    provideIdioma(),
    // La ayuda y los errores de un campo ocupan lo que necesiten: con la altura fija se cortaban en las pantallas angostas.
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { subscriptSizing: 'dynamic' } satisfies MatFormFieldDefaultOptions },
    provideAppInitializer(() => registrarIconos(inject(MatIconRegistry), inject(DomSanitizer))),
  ],
};

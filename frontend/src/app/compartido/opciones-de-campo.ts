import { MAT_FORM_FIELD_DEFAULT_OPTIONS, MatFormFieldDefaultOptions } from '@angular/material/form-field';

/**
 * Cómo se ven los campos de Material en las páginas con formularios: la ayuda y los errores de un campo ocupan lo que
 * necesiten (con la altura fija se cortaban en las pantallas angostas). Va en los `providers` de cada página y no en
 * app.config.ts: ahí obligaba a cargar Material form-field y Angular Forms en el paquete inicial, y la portada no usa
 * ningún campo.
 */
export const OPCIONES_DE_CAMPO = {
  provide: MAT_FORM_FIELD_DEFAULT_OPTIONS,
  useValue: { subscriptSizing: 'dynamic' } satisfies MatFormFieldDefaultOptions,
};

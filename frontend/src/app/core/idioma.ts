import { registerLocaleData } from '@angular/common';
import localeEsMx from '@angular/common/locales/es-MX';
import { LOCALE_ID, Provider } from '@angular/core';

/** Fechas y números en español de México: "20 de noviembre de 2026" y "52,513". */
export function provideIdioma(): Provider {
  registerLocaleData(localeEsMx);
  return { provide: LOCALE_ID, useValue: 'es-MX' };
}

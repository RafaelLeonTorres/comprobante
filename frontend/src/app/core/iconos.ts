import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

/**
 * Iconos de Material (Apache 2.0) como SVG en línea. La app no pide fuentes de iconos a ningún sitio externo, lo que
 * deja la política de seguridad de contenido (CSP) de nginx sin excepciones.
 */
const ICONOS: Readonly<Record<string, string>> = {
  documento:
    'M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z',
  correcto:
    'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z',
  error:
    'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z',
  advertencia: 'M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z',
  informacion:
    'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z',
};

export function registrarIconos(registro: MatIconRegistry, sanitizador: DomSanitizer): void {
  for (const [nombre, trazo] of Object.entries(ICONOS)) {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor"><path d="${trazo}"/></svg>`;
    registro.addSvgIconLiteral(nombre, sanitizador.bypassSecurityTrustHtml(svg));
  }
}

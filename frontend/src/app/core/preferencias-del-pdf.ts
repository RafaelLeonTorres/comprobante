import { Injectable, signal } from '@angular/core';

/**
 * Lo último que se eligió para el PDF, mientras la página siga abierta: al volver a descargar no hay que subir el logo otra
 * vez. No se guarda en el navegador ni se manda a ningún lado hasta que se pide el PDF.
 */
@Injectable({ providedIn: 'root' })
export class PreferenciasDelPdf {
  readonly logo = signal<File | null>(null);
  readonly incluirCadenaYSellos = signal(true);
}

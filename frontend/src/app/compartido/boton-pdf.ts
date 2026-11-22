import { Component, OnDestroy, computed, inject, input, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';

import { LIMITES_DEL_PDF } from '../core/api-comprobante';
import { DatosDelDialogoPdf, DialogoPdf, ResultadoDelDialogoPdf } from './dialogo-pdf';

let siguiente = 0;

/**
 * El botón «Descargar PDF» de un XML: abre el diálogo con las opciones del PDF y, al descargar, lo avisa un momento (también
 * a los lectores de pantalla). Si no se puede descargar se queda a la vista, con el motivo al lado, en lugar de desaparecer.
 */
@Component({
  selector: 'app-boton-pdf',
  imports: [MatButton, MatIcon],
  template: `
    <button
      matButton="outlined"
      type="button"
      aria-haspopup="dialog"
      disabledInteractive
      [disabled]="!disponible()"
      [attr.aria-describedby]="motivoActual() ? idDelMotivo : null"
      (click)="abrir()"
    >
      <mat-icon svgIcon="pdf" aria-hidden="true" />
      Descargar PDF
    </button>
    @if (motivoActual(); as motivo) {
      <span class="nota" [id]="idDelMotivo">{{ motivo }}</span>
    }
    <span class="nota" role="status">{{ aviso() }}</span>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex-wrap: wrap;
      align-items: center;
    }

    /* La región de estado siempre está en la página (así se anuncia lo que escribe), pero vacía no ocupa lugar. */
    .nota {
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }

    .nota:not(:empty) {
      margin-inline-start: 12px;
    }
  `,
})
export class BotonPdf implements OnDestroy {
  private readonly dialogo = inject(MatDialog);
  private temporizador: ReturnType<typeof setTimeout> | undefined;

  /** El XML del que se dibuja el PDF. */
  readonly xml = input.required<string>();
  /** Falso cuando el XML todavía no debe convertirse (por ejemplo, si tiene errores). */
  readonly habilitado = input(true);
  /** Por qué no está habilitado; se muestra junto al botón. */
  readonly motivo = input('');

  protected readonly aviso = signal('');
  protected readonly idDelMotivo = `motivo-pdf-${siguiente++}`;

  private readonly cabe = computed(() => new Blob([this.xml()]).size <= LIMITES_DEL_PDF.xml);
  protected readonly disponible = computed(() => this.habilitado() && this.cabe());
  protected readonly motivoActual = computed(() => {
    if (!this.habilitado()) {
      return this.motivo();
    }

    return this.cabe() ? '' : `El XML pesa más de ${LIMITES_DEL_PDF.xml / 1024 / 1024} MB, que es lo que admite el PDF.`;
  });

  protected async abrir(): Promise<void> {
    if (!this.disponible()) {
      return;
    }

    const referencia = this.dialogo.open<DialogoPdf, DatosDelDialogoPdf, ResultadoDelDialogoPdf>(DialogoPdf, {
      data: { xml: this.xml() },
      width: '36rem',
      maxWidth: 'calc(100vw - 32px)',
    });

    const resultado = await firstValueFrom(referencia.afterClosed());
    if (resultado) {
      this.aviso.set(`Se descargó ${resultado.nombre}.`);
      clearTimeout(this.temporizador);
      this.temporizador = setTimeout(() => this.aviso.set(''), 8000);
    }
  }

  ngOnDestroy(): void {
    clearTimeout(this.temporizador);
  }
}

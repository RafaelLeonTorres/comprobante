import { Component, OnDestroy, input, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';

/** Copia un texto al portapapeles y lo avisa un momento, también a los lectores de pantalla. */
@Component({
  selector: 'app-boton-copiar',
  imports: [MatButton, MatIcon],
  template: `
    <button matButton="outlined" type="button" (click)="copiar()">
      <mat-icon svgIcon="copiar" aria-hidden="true" />
      {{ copiado() ? 'Copiado' : etiqueta() }}
    </button>
    <span class="solo-lectores" role="status">{{ copiado() ? 'Copiado al portapapeles.' : '' }}</span>
  `,
  styles: `
    .solo-lectores {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
  `,
})
export class BotonCopiar implements OnDestroy {
  readonly texto = input.required<string>();
  readonly etiqueta = input('Copiar');

  protected readonly copiado = signal(false);
  private temporizador: ReturnType<typeof setTimeout> | undefined;

  protected async copiar(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.texto());
    } catch {
      // Sin permiso o sin contexto seguro: se usa la selección de un textarea, que funciona en todos lados.
      const campo = document.createElement('textarea');
      campo.value = this.texto();
      campo.setAttribute('readonly', '');
      campo.style.position = 'fixed';
      campo.style.opacity = '0';
      document.body.append(campo);
      campo.select();
      document.execCommand('copy');
      campo.remove();
    }

    this.copiado.set(true);
    clearTimeout(this.temporizador);
    this.temporizador = setTimeout(() => this.copiado.set(false), 2000);
  }

  ngOnDestroy(): void {
    clearTimeout(this.temporizador);
  }
}

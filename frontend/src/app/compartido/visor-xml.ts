import { Component, ElementRef, computed, inject, input, signal } from '@angular/core';

import { resaltarXml } from './resaltado-xml';

/**
 * Muestra un XML con números de línea, resaltado de sintaxis y las líneas con hallazgos marcadas. Es solo lectura: para
 * copiarlo o descargarlo, la pantalla que lo usa trae sus botones. <c>enfocarLinea</c> lleva la vista a una línea.
 */
@Component({
  selector: 'app-visor-xml',
  template: `
    <div class="marco" role="region" tabindex="0" [attr.aria-label]="etiqueta()">
      <pre><code>@for (linea of lineas(); track $index) {<span class="linea" [class.con-hallazgo]="marcadas().has($index + 1)" [class.activa]="activa() === $index + 1" [attr.data-linea]="$index + 1">@for (token of linea; track $index) {<span [class]="token.tipo">{{ token.texto }}</span>}</span>}</code></pre>
    </div>
    <p class="solo-lectores" role="status">{{ anuncio() }}</p>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }

    .marco {
      max-height: var(--altura-visor, 32rem);
      overflow: auto;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-medium);
      background-color: var(--mat-sys-surface-container-lowest);
      font-size: 0.8125rem;
      line-height: 1.55;
    }

    pre {
      width: max-content;
      min-width: 100%;
      margin: 0;
      padding: 8px 0;
      counter-reset: linea;
    }

    code {
      font-family: inherit;
    }

    .linea {
      display: block;
      min-height: 1.55em;
      padding-right: 16px;
      counter-increment: linea;
      color: var(--mat-sys-on-surface);
      white-space: pre;

      &::before {
        content: counter(linea);
        display: inline-block;
        box-sizing: border-box;
        width: 4.25em;
        padding-right: 1.25em;
        text-align: right;
        color: var(--mat-sys-on-surface-variant);
        user-select: none;
      }

      &.con-hallazgo {
        background-color: color-mix(in srgb, var(--mat-sys-error) 14%, transparent);
        box-shadow: inset 3px 0 0 var(--mat-sys-error);
      }

      &.activa {
        outline: 2px solid var(--mat-sys-primary);
        outline-offset: -2px;
      }
    }

    .etiqueta {
      color: var(--mat-sys-primary);
    }

    .atributo {
      color: var(--mat-sys-tertiary);
    }

    .valor {
      color: var(--mat-sys-secondary);
    }

    .puntuacion {
      color: var(--mat-sys-on-surface-variant);
    }

    .comentario,
    .declaracion {
      color: var(--mat-sys-on-surface-variant);
      font-style: italic;
    }

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
export class VisorXml {
  private readonly anfitrion = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly xml = input.required<string>();
  /** Lo que lee un lector de pantalla al llegar a la región: "XML generado", "XML a validar"... */
  readonly etiqueta = input('XML');
  /** Números de línea (desde 1) que tienen algún hallazgo. */
  readonly lineasConHallazgos = input<readonly number[]>([]);

  protected readonly lineas = computed(() => resaltarXml(this.xml()));
  protected readonly marcadas = computed(() => new Set(this.lineasConHallazgos()));
  protected readonly activa = signal<number | null>(null);
  protected readonly anuncio = signal('');

  /** Lleva la vista a la línea (desde 1), la resalta y la anuncia a los lectores de pantalla. */
  enfocarLinea(linea: number): void {
    const fila = this.anfitrion.nativeElement.querySelector<HTMLElement>(`[data-linea="${linea}"]`);
    if (!fila) {
      return;
    }

    this.activa.set(linea);
    this.anuncio.set(`Línea ${linea}: ${(fila.textContent ?? '').trim().slice(0, 160)}`);
    fila.scrollIntoView({ block: 'center', inline: 'nearest' });
  }
}

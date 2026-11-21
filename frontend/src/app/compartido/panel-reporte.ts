import { DatePipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { MatIcon } from '@angular/material/icon';

import { Hallazgo, ReporteValidacion } from '../core/modelos';
import { BotonCopiar } from './boton-copiar';

interface GrupoDeHallazgos {
  readonly nodo: string;
  readonly nombre: string;
  readonly ruta: string;
  readonly hallazgos: readonly Hallazgo[];
}

/** El último tramo de la ruta es el nodo; lo anterior es dónde está. */
function separarRuta(nodo: string): { nombre: string; ruta: string } {
  const indice = nodo.lastIndexOf('/');
  return indice === -1 ? { nombre: nodo || 'Documento', ruta: '' } : { nombre: nodo.slice(indice + 1), ruta: nodo.slice(0, indice) };
}

/**
 * El resultado de validar un XML: el veredicto, el estado del sello y del certificado, los hallazgos agrupados por nodo con su
 * código del SAT y la cadena original. Lo usan la pantalla de validar y el resultado de generar.
 */
@Component({
  selector: 'app-panel-reporte',
  imports: [BotonCopiar, DatePipe, MatIcon],
  template: `
    @let r = reporte();
    <div class="veredicto" [class.correcto]="r.esValido && !advertencias()" [class.con-error]="!r.esValido" role="status">
      <mat-icon [svgIcon]="r.esValido ? (advertencias() ? 'advertencia' : 'correcto') : 'error'" aria-hidden="true" />
      <div>
        <p class="titulo">
          @if (!r.esValido) {
            El XML tiene {{ errores() === 1 ? '1 error' : errores() + ' errores' }}
            @if (advertencias()) {
              y {{ advertencias() === 1 ? '1 advertencia' : advertencias() + ' advertencias' }}
            }
          } @else if (advertencias()) {
            Sin errores, con {{ advertencias() === 1 ? '1 advertencia' : advertencias() + ' advertencias' }}
          } @else {
            Sin hallazgos: el XML cumple lo que revisa esta app
          }
        </p>
        <p class="detalle">
          @switch (r.selloValido) {
            @case (true) {
              El sello corresponde a la cadena original y al certificado del comprobante.
            }
            @case (false) {
              El sello no corresponde a la cadena original de este XML con el certificado que trae.
            }
            @default {
              No se pudo comprobar el sello: falta el sello, el certificado o la cadena original.
            }
          }
        </p>
      </div>
    </div>

    @if (r.certificado; as c) {
      <p class="certificado">
        Certificado <span class="mono">{{ c.noCertificado }}</span>
        @if (c.rfc) {
          de <span class="mono">{{ c.rfc }}</span>
        }
        , vigente del {{ c.validoDesde | date: 'mediumDate' : 'UTC' }} al {{ c.validoHasta | date: 'mediumDate' : 'UTC' }}.
      </p>
    }

    @for (grupo of grupos(); track grupo.nodo) {
      <section class="grupo" [attr.aria-label]="'Hallazgos de ' + grupo.nombre">
        <h3>
          <code>{{ grupo.nombre }}</code>
          @if (grupo.ruta) {
            <span class="ruta">en {{ grupo.ruta }}</span>
          }
        </h3>
        <ul>
          @for (h of grupo.hallazgos; track $index) {
            <li class="hallazgo" [class.advertencia]="h.severidad === 'advertencia'">
              <mat-icon [svgIcon]="h.severidad === 'error' ? 'error' : 'advertencia'" aria-hidden="true" />
              <div class="cuerpo">
                <p class="mensaje">
                  <span class="solo-lectores">{{ h.severidad === 'error' ? 'Error' : 'Advertencia' }}</span>
                  <strong class="codigo">{{ h.codigo }}</strong>
                  {{ h.mensaje }}
                </p>
                <p class="meta">
                  @if (h.atributo) {
                    <span>Atributo <code>{{ h.atributo }}</code></span>
                  }
                  @if (h.encontrado) {
                    <span>Encontrado <code>{{ h.encontrado }}</code></span>
                  }
                  @if (h.esperado) {
                    <span>Esperado <code>{{ h.esperado }}</code></span>
                  }
                  @if (h.linea) {
                    <button type="button" class="linea" (click)="irALinea.emit(h.linea!)">Línea {{ h.linea }}</button>
                  }
                </p>
              </div>
            </li>
          }
        </ul>
      </section>
    }

    @if (r.cadenaOriginal) {
      <details class="cadena">
        <summary>Cadena original</summary>
        <pre class="mono" tabindex="0" aria-label="Cadena original">{{ r.cadenaOriginal }}</pre>
        <app-boton-copiar [texto]="r.cadenaOriginal" etiqueta="Copiar cadena original" />
      </details>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .veredicto {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      padding: 16px;
      border-radius: var(--mat-sys-corner-medium);
      background-color: var(--mat-sys-surface-container-high);

      &.correcto {
        background-color: var(--mat-sys-primary-container);
        color: var(--mat-sys-on-primary-container);
      }

      &.con-error {
        background-color: var(--mat-sys-error-container);
        color: var(--mat-sys-on-error-container);
      }

      p {
        margin: 0;
      }

      .titulo {
        font: var(--mat-sys-title-medium);
      }

      .detalle {
        margin-top: 4px;
      }
    }

    .certificado {
      margin: 12px 0 0;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }

    .grupo {
      margin-top: 20px;

      h3 {
        display: flex;
        flex-wrap: wrap;
        gap: 4px 12px;
        align-items: baseline;
        margin: 0 0 8px;
        font: var(--mat-sys-title-small);
      }

      .ruta {
        font: var(--mat-sys-body-small);
        color: var(--mat-sys-on-surface-variant);
        overflow-wrap: anywhere;
      }

      ul {
        display: grid;
        gap: 8px;
        margin: 0;
        padding: 0;
        list-style: none;
      }
    }

    .hallazgo {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      padding: 12px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-left: 4px solid var(--mat-sys-error);
      border-radius: var(--mat-sys-corner-small);

      &.advertencia {
        border-left-color: var(--mat-sys-tertiary);
      }

      mat-icon {
        flex: none;
        color: var(--mat-sys-error);
      }

      &.advertencia mat-icon {
        color: var(--mat-sys-tertiary);
      }

      .cuerpo {
        min-width: 0;
      }

      p {
        margin: 0;
      }

      .codigo {
        margin-right: 6px;
        padding: 1px 6px;
        border-radius: var(--mat-sys-corner-extra-small);
        background-color: var(--mat-sys-surface-container-highest);
        font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace;
        font-size: 0.8125rem;
      }

      .meta {
        display: flex;
        flex-wrap: wrap;
        gap: 4px 16px;
        align-items: center;
        margin-top: 6px;
        font: var(--mat-sys-body-small);
        color: var(--mat-sys-on-surface-variant);
        overflow-wrap: anywhere;
      }
    }

    .linea {
      padding: 0 4px;
      border: 0;
      background: none;
      font: inherit;
      color: var(--mat-sys-primary);
      text-decoration: underline;
      cursor: pointer;
    }

    .cadena {
      margin-top: 20px;

      summary {
        cursor: pointer;
        font: var(--mat-sys-title-small);
      }

      pre {
        max-height: 14rem;
        margin: 12px 0;
        padding: 12px;
        overflow: auto;
        border: 1px solid var(--mat-sys-outline-variant);
        border-radius: var(--mat-sys-corner-small);
        background-color: var(--mat-sys-surface-container-lowest);
        font-size: 0.8125rem;
        white-space: pre-wrap;
        overflow-wrap: anywhere;
      }
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
export class PanelReporte {
  readonly reporte = input.required<ReporteValidacion>();
  /** El usuario pidió ver una línea del XML: la pantalla que lo usa la enseña en el visor. */
  readonly irALinea = output<number>();

  protected readonly errores = computed(() => this.reporte().hallazgos.filter((h) => h.severidad === 'error').length);
  protected readonly advertencias = computed(() => this.reporte().hallazgos.length - this.errores());

  protected readonly grupos = computed<readonly GrupoDeHallazgos[]>(() => {
    const porNodo = new Map<string, Hallazgo[]>();
    for (const hallazgo of this.reporte().hallazgos) {
      const lista = porNodo.get(hallazgo.nodo);
      if (lista) {
        lista.push(hallazgo);
      } else {
        porNodo.set(hallazgo.nodo, [hallazgo]);
      }
    }

    return [...porNodo].map(([nodo, hallazgos]) => ({ nodo, ...separarRuta(nodo), hallazgos }));
  });
}

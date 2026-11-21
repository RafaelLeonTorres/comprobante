import { Component, computed, input, viewChild } from '@angular/core';

import { ReporteValidacion } from '../core/modelos';
import { BotonCopiar } from './boton-copiar';
import { PanelReporte } from './panel-reporte';
import { VisorXml } from './visor-xml';

/**
 * Un XML junto con el reporte de su validación: el reporte a un lado, el XML con sus líneas marcadas al otro, y cada hallazgo
 * lleva a su línea. Lo usan la pantalla de validar y la de generar. Lo que se proyecte dentro (botones, por ejemplo) queda
 * junto al botón de copiar.
 */
@Component({
  selector: 'app-revision-xml',
  imports: [BotonCopiar, PanelReporte, VisorXml],
  template: `
    <div class="columnas">
      <app-panel-reporte [reporte]="reporte()" (irALinea)="visor()?.enfocarLinea($event)" />
      <div class="xml">
        <div class="encabezado">
          <h3>{{ etiqueta() }}</h3>
          <div class="acciones">
            <ng-content />
            <app-boton-copiar [texto]="xml()" etiqueta="Copiar XML" />
          </div>
        </div>
        <app-visor-xml [xml]="xml()" [lineasConHallazgos]="lineasMarcadas()" [etiqueta]="etiqueta()" />
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    /* minmax(0, 1fr): sin el cero, una línea larga de XML ensancharía la columna (y la página) en lugar de usar el scroll del visor. */
    .columnas {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: 24px;
      align-items: start;
    }

    h3 {
      margin: 0;
      font: var(--mat-sys-title-medium);
    }

    .encabezado {
      display: flex;
      flex-wrap: wrap;
      gap: 8px 16px;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 12px;
    }

    .acciones {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      align-items: center;
    }

    @media (min-width: 64rem) {
      .columnas {
        grid-template-columns: minmax(0, 5fr) minmax(0, 6fr);
      }

      .xml {
        position: sticky;
        top: 16px;
      }
    }
  `,
})
export class RevisionXml {
  readonly xml = input.required<string>();
  readonly reporte = input.required<ReporteValidacion>();
  readonly etiqueta = input('XML revisado');

  protected readonly visor = viewChild(VisorXml);

  /** Las líneas del XML con algún hallazgo, para marcarlas en el visor. */
  protected readonly lineasMarcadas = computed(() => [
    ...new Set(this.reporte().hallazgos.map((h) => h.linea).filter((linea): linea is number => !!linea)),
  ]);
}

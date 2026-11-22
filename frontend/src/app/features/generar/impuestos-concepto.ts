import { Component, computed, input } from '@angular/core';
import { FieldTree, FormField } from '@angular/forms/signals';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatOption } from '@angular/material/core';
import { MatError, MatFormField, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatSelect } from '@angular/material/select';

import { ImpuestoCalculado } from '../../core/modelos';
import { importe, porcentaje } from '../../core/formato';
import { primerMensaje } from './campos';
import { ImpuestoFormulario, impuestoNuevo } from './formulario';

interface OpcionDeImpuesto {
  clave: string;
  nombre: string;
}

const IMPUESTOS_TRASLADABLES: readonly OpcionDeImpuesto[] = [
  { clave: '002', nombre: 'IVA' },
  { clave: '003', nombre: 'IEPS' },
];

const IMPUESTOS_RETENIBLES: readonly OpcionDeImpuesto[] = [
  { clave: '001', nombre: 'ISR' },
  { clave: '002', nombre: 'IVA' },
  { clave: '003', nombre: 'IEPS' },
];

/** Los traslados o las retenciones de un concepto: un renglón por impuesto, con su importe calculado. */
@Component({
  selector: 'app-impuestos-concepto',
  imports: [FormField, MatButton, MatError, MatFormField, MatIcon, MatIconButton, MatInput, MatLabel, MatOption, MatSelect, MatSuffix],
  template: `
    <div class="bloque" role="group" [attr.aria-label]="titulo()">
      <div class="encabezado">
        <h4>{{ titulo() }}</h4>
        <button matButton type="button" (click)="agregar()">
          <mat-icon svgIcon="agregar" aria-hidden="true" />
          Agregar {{ singular() }}
        </button>
      </div>

      @for (fila of impuestos(); track fila; let i = $index) {
        <div class="fila" role="group" [attr.aria-label]="nombreDelRenglon(i)">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Impuesto</mat-label>
            <mat-select [formField]="fila.impuesto">
              @for (opcion of opciones(); track opcion.clave) {
                <mat-option [value]="opcion.clave">{{ opcion.clave }} · {{ opcion.nombre }}</mat-option>
              }
            </mat-select>
            <mat-error>{{ primerMensaje(fila.impuesto) }}</mat-error>
          </mat-form-field>

          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Factor</mat-label>
            <mat-select [formField]="fila.tipoFactor">
              <mat-option value="Tasa">Tasa</mat-option>
              <mat-option value="Cuota">Cuota</mat-option>
              @if (tipo() === 'traslado') {
                <mat-option value="Exento">Exento</mat-option>
              }
            </mat-select>
            <mat-error>{{ primerMensaje(fila.tipoFactor) }}</mat-error>
          </mat-form-field>

          @if (!fila.tasaOCuota().hidden()) {
            <mat-form-field class="tasa" subscriptSizing="dynamic">
              <mat-label>Tasa o cuota</mat-label>
              <input matInput type="number" inputmode="decimal" step="any" [formField]="fila.tasaOCuota" />
              @if (porcentajeDe(fila); as texto) {
                <span matTextSuffix class="porcentaje">{{ texto }}</span>
              }
              <mat-error>{{ primerMensaje(fila.tasaOCuota) }}</mat-error>
            </mat-form-field>
          } @else {
            <p class="exento">Sin tasa ni cuota</p>
          }

          <div class="fin">
            <p class="importe">{{ importeDe(i, fila) }}</p>
            <button matIconButton type="button" [attr.aria-label]="'Quitar ' + nombreDelRenglon(i)" (click)="quitar(i)">
              <mat-icon svgIcon="borrar" aria-hidden="true" />
            </button>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      margin-top: 16px;
    }

    .encabezado {
      display: flex;
      flex-wrap: wrap;
      gap: 4px 16px;
      align-items: center;
      justify-content: space-between;
    }

    h4 {
      margin: 0;
      font: var(--mat-sys-title-small);
    }

    .fila {
      display: flex;
      flex-wrap: wrap;
      gap: 8px 12px;
      align-items: flex-start;
      margin-top: 8px;

      mat-form-field,
      .exento {
        flex: 1 1 9rem;
        min-width: 0;
      }
    }

    /* La tasa lleva el porcentaje al lado (16 %): necesita más sitio que los selectores y el sufijo no debe partirse en dos líneas. */
    .fila mat-form-field.tasa {
      flex-basis: 11rem;
    }

    .porcentaje {
      padding-inline-start: 8px;
      white-space: nowrap;
    }

    .exento {
      margin: 0;
      padding-top: 16px;
      color: var(--mat-sys-on-surface-variant);
    }

    /* El importe y el botón de quitar van juntos al final del renglón, aunque los campos pasen a varias filas. */
    .fin {
      display: flex;
      gap: 4px;
      align-items: center;
      align-self: flex-start;
      min-height: 56px;
      margin-inline-start: auto;
    }

    .importe {
      min-width: 6rem;
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
      font-variant-numeric: tabular-nums;
      text-align: end;
      white-space: nowrap;
    }
  `,
})
export class ImpuestosConcepto {
  readonly impuestos = input.required<FieldTree<ImpuestoFormulario[]>>();
  readonly tipo = input.required<'traslado' | 'retencion'>();
  /** Los importes que calculó la API para este concepto, en el mismo orden que los renglones. */
  readonly calculados = input<readonly ImpuestoCalculado[]>([]);
  readonly decimales = input(2);

  protected readonly primerMensaje = primerMensaje;
  protected readonly titulo = computed(() => (this.tipo() === 'traslado' ? 'Impuestos trasladados' : 'Impuestos retenidos'));
  protected readonly singular = computed(() => (this.tipo() === 'traslado' ? 'traslado' : 'retención'));
  protected readonly opciones = computed(() => (this.tipo() === 'traslado' ? IMPUESTOS_TRASLADABLES : IMPUESTOS_RETENIBLES));

  protected nombreDelRenglon(indice: number): string {
    return `${this.tipo() === 'traslado' ? 'Traslado' : 'Retención'} ${indice + 1}`;
  }

  /** La tasa como porcentaje junto al campo: si se escribió 16 en vez de 0.16, se lee "1,600 %" y se nota. */
  protected porcentajeDe(fila: FieldTree<ImpuestoFormulario>): string {
    const tasa = fila.tasaOCuota().value();
    return fila.tipoFactor().value() === 'Tasa' && tasa !== null ? porcentaje(tasa) : '';
  }

  protected importeDe(indice: number, fila: FieldTree<ImpuestoFormulario>): string {
    if (fila.tipoFactor().value() === 'Exento') {
      return 'Exento';
    }

    const calculado = this.calculados()[indice];
    return calculado?.importe == null ? '' : importe(calculado.importe, this.decimales());
  }

  protected agregar(): void {
    this.impuestos()().value.update((lista) => [...lista, impuestoNuevo(this.tipo())]);
  }

  protected quitar(indice: number): void {
    this.impuestos()().value.update((lista) => lista.filter((_, posicion) => posicion !== indice));
  }
}

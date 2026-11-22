import { Component, computed, input, output } from '@angular/core';
import { FieldTree, FormField } from '@angular/forms/signals';
import { MatButton } from '@angular/material/button';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';

import { CampoCatalogo } from '../../compartido/campo-catalogo';
import { importe } from '../../core/formato';
import { ConceptoCalculado, FilaCatalogo } from '../../core/modelos';
import { primerMensaje } from './campos';
import { ConceptoFormulario } from './formulario';
import { ImpuestosConcepto } from './impuestos-concepto';

let siguiente = 0;

/** Un concepto del comprobante: lo que se vende, cuánto y con qué impuestos. */
@Component({
  selector: 'app-tarjeta-concepto',
  imports: [CampoCatalogo, FormField, ImpuestosConcepto, MatButton, MatError, MatFormField, MatHint, MatIcon, MatInput, MatLabel],
  template: `
    <section class="concepto" [attr.aria-labelledby]="idTitulo">
      <header>
        <h3 [id]="idTitulo">Concepto {{ numero() }}</h3>
        @if (importeDelConcepto(); as total) {
          <p class="importe">Importe <strong>{{ total }}</strong></p>
        }
        @if (puedeQuitar()) {
          <button matButton type="button" (click)="quitar.emit()">
            <mat-icon svgIcon="borrar" aria-hidden="true" />
            Quitar concepto
          </button>
        }
      </header>

      <div class="rejilla">
        <app-campo-catalogo
          [campo]="concepto().claveProdServ"
          catalogo="clave-prod-serv"
          etiqueta="Producto o servicio"
          ayuda="Escribe 3 caracteres para buscar"
          [minimo]="3"
        />
        <app-campo-catalogo
          [campo]="concepto().claveUnidad"
          catalogo="clave-unidad"
          etiqueta="Clave de unidad"
          ayuda="Por ejemplo H87 o E48"
          [minimo]="1"
          (seleccionada)="alElegirUnidad($event)"
        />
        <mat-form-field>
          <mat-label>Unidad</mat-label>
          <input matInput type="text" autocomplete="off" [formField]="concepto().unidad" />
          <mat-hint>Texto libre, como «Pieza»</mat-hint>
        </mat-form-field>
        <mat-form-field>
          <mat-label>No. de identificación</mat-label>
          <input matInput type="text" autocomplete="off" [formField]="concepto().noIdentificacion" />
        </mat-form-field>
        <mat-form-field class="completo">
          <mat-label>Descripción</mat-label>
          <textarea matInput rows="2" [formField]="concepto().descripcion"></textarea>
          <mat-error>{{ primerMensaje(concepto().descripcion) }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Cantidad</mat-label>
          <input matInput type="number" inputmode="decimal" step="any" [formField]="concepto().cantidad" />
          <mat-hint>Hasta 6 decimales</mat-hint>
          <mat-error>{{ primerMensaje(concepto().cantidad) }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Valor unitario</mat-label>
          <input matInput type="number" inputmode="decimal" step="any" [formField]="concepto().valorUnitario" />
          <mat-hint>Sin impuestos</mat-hint>
          <mat-error>{{ primerMensaje(concepto().valorUnitario) }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Descuento</mat-label>
          <input matInput type="number" inputmode="decimal" step="any" [formField]="concepto().descuento" />
          <mat-hint>Opcional</mat-hint>
          <mat-error>{{ primerMensaje(concepto().descuento) }}</mat-error>
        </mat-form-field>
      </div>

      <app-impuestos-concepto [impuestos]="concepto().traslados" tipo="traslado" [calculados]="calculado()?.traslados ?? []" [decimales]="decimales()" />
      <app-impuestos-concepto [impuestos]="concepto().retenciones" tipo="retencion" [calculados]="calculado()?.retenciones ?? []" [decimales]="decimales()" />
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .concepto {
      margin-bottom: 16px;
      padding: 16px;
      border-radius: var(--mat-sys-corner-medium);
      background-color: var(--mat-sys-surface-container-low);
    }

    header {
      display: flex;
      flex-wrap: wrap;
      gap: 4px 16px;
      align-items: center;
      margin-bottom: 12px;
    }

    h3 {
      margin: 0;
      font: var(--mat-sys-title-medium);
    }

    .importe {
      flex: 1;
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
      font-variant-numeric: tabular-nums;
    }

  `,
})
export class TarjetaConcepto {
  readonly concepto = input.required<FieldTree<ConceptoFormulario>>();
  readonly numero = input.required<number>();
  /** Lo que calculó la API de este concepto; vacío mientras no hay con qué calcular. */
  readonly calculado = input<ConceptoCalculado | undefined>(undefined);
  readonly decimales = input(2);
  readonly puedeQuitar = input(false);

  readonly quitar = output();

  protected readonly idTitulo = `concepto-${++siguiente}`;
  protected readonly primerMensaje = primerMensaje;

  protected readonly importeDelConcepto = computed(() => {
    const calculado = this.calculado();
    return calculado ? importe(calculado.importe, this.decimales()) : '';
  });

  /** Al elegir una clave de unidad, si el campo «Unidad» está vacío se llena con su nombre. */
  protected alElegirUnidad(fila: FilaCatalogo): void {
    const unidad = this.concepto().unidad();
    if (unidad.value().trim() === '' && typeof fila['nombre'] === 'string') {
      unidad.value.set(fila['nombre']);
    }
  }
}

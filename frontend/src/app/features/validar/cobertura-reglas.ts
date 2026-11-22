import { httpResource } from '@angular/common/http';
import { Component, computed, signal } from '@angular/core';
import { FormField, form } from '@angular/forms/signals';
import { MatButtonToggle, MatButtonToggleGroup } from '@angular/material/button-toggle';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';

import { CoberturaDeReglas, EstadoDeRegla } from '../../core/modelos';

const ESTADOS: Readonly<Record<EstadoDeRegla, { etiqueta: string; icono: string }>> = {
  revisada: { etiqueta: 'Revisada', icono: 'correcto' },
  parcial: { etiqueta: 'Parcial', icono: 'advertencia' },
  noRevisada: { etiqueta: 'No revisada', icono: 'informacion' },
};

/**
 * Las reglas de la matriz de errores del SAT y lo que hace esta app con cada una. Sirve para saber qué significa "sin
 * hallazgos": una regla que necesita datos que la app no tiene sin conexión (la lista de RFC inscritos, el tipo de cambio del
 * DOF) aparece como no revisada y con su motivo, en lugar de aprobarse en silencio. Se pide a la API al abrir el bloque.
 */
@Component({
  selector: 'app-cobertura-reglas',
  imports: [FormField, MatButtonToggle, MatButtonToggleGroup, MatFormField, MatIcon, MatInput, MatLabel],
  template: `
    <details class="reglas" (toggle)="alAbrir($event)">
      <summary>Ver las reglas de la matriz de errores y qué hace la app con cada una</summary>

      @if (cobertura.hasValue()) {
        @let datos = cobertura.value()!;
        <p class="resumen">
          De las {{ datos.resumen.total }} reglas de la matriz, la app revisa {{ datos.resumen.revisadas }} completas y
          {{ datos.resumen.parciales }} en parte; las otras {{ datos.resumen.noRevisadas }} necesitan datos que no tiene sin
          conexión (la lista de RFC inscritos, el tipo de cambio del DOF, lo que verifica el PAC al timbrar).
        </p>

        <div class="filtros">
          <mat-button-toggle-group [formField]="filtros.estado" aria-label="Filtrar las reglas por estado">
            <mat-button-toggle value="todas">Todas</mat-button-toggle>
            <mat-button-toggle value="revisada">Revisadas</mat-button-toggle>
            <mat-button-toggle value="parcial">Parciales</mat-button-toggle>
            <mat-button-toggle value="noRevisada">No revisadas</mat-button-toggle>
          </mat-button-toggle-group>

          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Buscar por código o texto</mat-label>
            <input matInput type="search" autocomplete="off" [formField]="filtros.texto" />
          </mat-form-field>
        </div>

        <p class="conteo" role="status">
          {{ visibles().length === 1 ? 'Se muestra 1 regla.' : 'Se muestran ' + visibles().length + ' reglas.' }}
        </p>

        <div class="tabla" role="region" tabindex="0" aria-label="Reglas de la matriz de errores del SAT">
          <table>
            <thead>
              <tr>
                <th scope="col">Código</th>
                <th scope="col">Nodo y atributo</th>
                <th scope="col">Qué revisa el SAT</th>
                <th scope="col">En esta app</th>
              </tr>
            </thead>
            <tbody>
              @for (regla of visibles(); track regla.codigo) {
                <tr>
                  <th scope="row" class="mono">{{ regla.codigo }}</th>
                  <td>
                    {{ regla.elemento }}@if (regla.atributo) {
                      <span class="atributo">{{ regla.atributo }}</span>
                    }
                  </td>
                  <td>{{ regla.descripcion }}</td>
                  <td>
                    <span class="estado" [class]="'estado ' + regla.estado">
                      <mat-icon [svgIcon]="estados[regla.estado].icono" aria-hidden="true" />
                      {{ estados[regla.estado].etiqueta }}
                    </span>
                    @if (regla.nota) {
                      <span class="nota">{{ regla.nota }}</span>
                    }
                    @if (regla.cubiertaPor) {
                      <span class="nota">Se informa con el código {{ regla.cubiertaPor }}.</span>
                    }
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="4">Ninguna regla coincide con el filtro.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      } @else if (cobertura.error()) {
        <p class="falla" role="alert">No se pudo cargar la lista de reglas. Vuelve a abrir este bloque en un momento.</p>
      } @else if (abierto()) {
        <p role="status">Cargando las reglas…</p>
      }
    </details>
  `,
  styles: `
    :host {
      display: block;
    }

    .reglas {
      padding: 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-medium);

      summary {
        cursor: pointer;
        font: var(--mat-sys-title-small);
      }
    }

    .resumen {
      margin: 16px 0;
      color: var(--mat-sys-on-surface-variant);
    }

    .filtros {
      display: flex;
      flex-wrap: wrap;
      gap: 12px 24px;
      align-items: center;
      margin-bottom: 8px;

      mat-form-field {
        flex: 1 1 16rem;
      }
    }

    .conteo {
      margin: 0 0 8px;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }

    .tabla {
      max-height: 32rem;
      overflow: auto;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-small);
    }

    table {
      width: 100%;
      min-width: 44rem;
      border-collapse: collapse;
      font: var(--mat-sys-body-small);
    }

    th,
    td {
      padding: 8px 12px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      text-align: left;
      vertical-align: top;
    }

    thead th {
      position: sticky;
      top: 0;
      background-color: var(--mat-sys-surface-container-high);
    }

    .atributo {
      display: block;
      color: var(--mat-sys-on-surface-variant);
    }

    .estado {
      display: inline-flex;
      gap: 4px;
      align-items: center;
      font-weight: 500;

      mat-icon {
        width: 18px;
        height: 18px;
      }

      &.revisada mat-icon {
        color: var(--mat-sys-primary);
      }

      &.parcial mat-icon {
        color: var(--mat-sys-tertiary);
      }
    }

    .nota {
      display: block;
      margin-top: 4px;
      color: var(--mat-sys-on-surface-variant);
    }

    .falla {
      color: var(--mat-sys-error);
    }
  `,
})
export class CoberturaReglas {
  protected readonly estados = ESTADOS;
  protected readonly abierto = signal(false);

  protected readonly filtros = form(signal({ estado: 'todas', texto: '' }));

  /** Se pide una sola vez, al abrir el bloque: son unos 40 KB que casi nadie necesita. */
  protected readonly cobertura = httpResource<CoberturaDeReglas>(() => (this.abierto() ? '/api/validaciones/reglas' : undefined));

  protected readonly visibles = computed(() => {
    const reglas = this.cobertura.hasValue() ? this.cobertura.value()!.reglas : [];
    const estado = this.filtros.estado().value();
    const texto = this.filtros.texto().value().trim().toLowerCase();
    return reglas.filter(
      (r) =>
        (estado === 'todas' || r.estado === estado) &&
        (!texto || `${r.codigo} ${r.elemento} ${r.atributo ?? ''} ${r.descripcion} ${r.nota ?? ''}`.toLowerCase().includes(texto)),
    );
  });

  protected alAbrir(evento: Event): void {
    if ((evento.target as HTMLDetailsElement).open) {
      this.abierto.set(true);
    }
  }
}

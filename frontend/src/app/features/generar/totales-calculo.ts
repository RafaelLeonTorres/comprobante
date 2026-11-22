import { Component, computed, input } from '@angular/core';

import { ErrorApi } from '../../core/errores-api';
import { importe, nombreDeImpuesto, porcentaje } from '../../core/formato';
import { ResultadoCalculo, TrasladoResumen } from '../../core/modelos';

export type EstadoDeTotales = 'vacio' | 'calculando' | 'listo' | 'error';

/** Subtotal, impuestos y total tal como los calcula la API con los redondeos del Anexo 20; se actualizan mientras se escribe. */
@Component({
  selector: 'app-totales-calculo',
  template: `
    <section class="totales" aria-labelledby="totales-titulo">
      <h2 id="totales-titulo">Totales</h2>

      <div aria-live="polite" aria-atomic="false">
        @switch (estado()) {
          @case ('vacio') {
            <p class="nota">Agrega un concepto con cantidad y valor unitario para ver los totales.</p>
          }
          @case ('error') {
            <p class="nota aviso">
              Todavía no se pueden calcular los totales.
              @if (primerMensaje(); as mensaje) {
                {{ mensaje }}
              }
            </p>
          }
          @default {
            @if (calculo(); as c) {
              <dl [class.actualizando]="estado() === 'calculando'">
                <div>
                  <dt>Subtotal</dt>
                  <dd>{{ importe(c.subTotal, c.decimalesMoneda) }}</dd>
                </div>
                @if (c.descuento != null) {
                  <div>
                    <dt>Descuento</dt>
                    <dd>−{{ importe(c.descuento, c.decimalesMoneda) }}</dd>
                  </div>
                }
                @for (traslado of c.traslados; track $index) {
                  <div>
                    <dt>
                      {{ nombreDelTraslado(traslado) }}
                      <small>base {{ importe(traslado.base, c.decimalesMoneda) }}</small>
                    </dt>
                    <dd>{{ traslado.importe == null ? '—' : importe(traslado.importe, c.decimalesMoneda) }}</dd>
                  </div>
                }
                @for (retencion of c.retenciones; track $index) {
                  <div>
                    <dt>{{ nombre(retencion.impuesto) }} retenido</dt>
                    <dd>−{{ importe(retencion.importe, c.decimalesMoneda) }}</dd>
                  </div>
                }
                <div class="total">
                  <dt>Total</dt>
                  <dd>{{ importe(c.total, c.decimalesMoneda) }} <span class="moneda">{{ c.moneda }}</span></dd>
                </div>
              </dl>
            } @else {
              <p class="nota">Calculando…</p>
            }
          }
        }
      </div>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    h2 {
      margin: 0 0 12px;
      font: var(--mat-sys-title-large);
    }

    dl {
      margin: 0;
      transition: opacity 150ms;

      &.actualizando {
        opacity: 0.6;
      }

      > div {
        display: flex;
        gap: 16px;
        justify-content: space-between;
        padding: 6px 0;
        border-bottom: 1px solid var(--mat-sys-outline-variant);
      }
    }

    dt small {
      display: block;
      color: var(--mat-sys-on-surface-variant);
    }

    dd {
      margin: 0;
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
    }

    .total {
      border-bottom: 0;
      font: var(--mat-sys-title-medium);

      .moneda {
        font: var(--mat-sys-label-medium);
        color: var(--mat-sys-on-surface-variant);
      }
    }

    .nota {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }

    .aviso {
      color: var(--mat-sys-on-error-container);
    }
  `,
})
export class TotalesCalculo {
  readonly estado = input.required<EstadoDeTotales>();
  readonly calculo = input<ResultadoCalculo | undefined>(undefined);
  readonly error = input<ErrorApi | null>(null);

  protected readonly importe = importe;
  protected readonly nombre = nombreDeImpuesto;

  /** Del último error, el primer mensaje: lo que haya que corregir para poder calcular. */
  protected readonly primerMensaje = computed(() => {
    const error = this.error();
    if (!error) {
      return '';
    }

    return Object.values(error.campos)[0]?.[0] ?? error.detalle ?? '';
  });

  protected nombreDelTraslado(traslado: TrasladoResumen): string {
    const nombre = nombreDeImpuesto(traslado.impuesto);
    switch (traslado.tipoFactor) {
      case 'Exento':
        return `${nombre} exento`;
      case 'Cuota':
        return `${nombre} cuota ${traslado.tasaOCuota ?? ''} trasladado`;
      default:
        return `${nombre} ${traslado.tasaOCuota == null ? '' : porcentaje(traslado.tasaOCuota)} trasladado`;
    }
  }
}

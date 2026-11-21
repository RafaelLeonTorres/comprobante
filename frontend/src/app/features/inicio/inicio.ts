import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { MatIcon } from '@angular/material/icon';

import { EstadoApi } from '../../core/estado-api';

@Component({
  selector: 'app-inicio',
  imports: [DatePipe, DecimalPipe, MatIcon],
  template: `
    <section class="portada">
      <h1>Generador y validador de XML CFDI 4.0</h1>
      <p class="resumen">
        Arma, sella y revisa comprobantes fiscales sin timbrarlos. Calcula importes e impuestos con los
        redondeos del Anexo 20, genera el XML con un certificado de prueba del SAT y revisa cualquier
        XML contra los esquemas y las reglas de validación, con el código de cada error.
      </p>
    </section>

    <aside class="aviso" role="note">
      <mat-icon svgIcon="advertencia" aria-hidden="true" />
      <p>
        <strong>Sin validez fiscal.</strong> La app no timbra ni envía nada al SAT ni a un PAC: los XML
        que genera no son facturas válidas.
      </p>
    </aside>

    <section class="estado" aria-labelledby="estado-titulo">
      <h2 id="estado-titulo">Estado del servicio</h2>

      @if (salud.hasValue() && salud.value()!.estado === 'ok') {
        <p class="fila correcto" role="status">
          <mat-icon svgIcon="correcto" aria-hidden="true" />
          <span>La API responde.</span>
        </p>
      } @else if (salud.error()) {
        <p class="fila error" role="alert">
          <mat-icon svgIcon="error" aria-hidden="true" />
          <span>
            La API no responde. Revisa que los contenedores estén corriendo
            (<code>docker compose ps</code>) y vuelve a cargar la página.
          </span>
        </p>
      } @else {
        <p class="fila" role="status">
          <mat-icon svgIcon="informacion" aria-hidden="true" />
          <span>Comprobando la API…</span>
        </p>
      }

      @if (catalogos.hasValue()) {
        @let resumen = catalogos.value()!;
        <p class="fila">
          <mat-icon svgIcon="informacion" aria-hidden="true" />
          <span>
            @if (resumen.version) {
              Catálogos del SAT publicados el {{ resumen.version.publicacion | date: 'longDate' }}
              ({{ resumen.version.archivo }}):
            }
            {{ resumen.catalogos.length }} catálogos y {{ totalFilas() | number }} filas.
          </span>
        </p>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
      box-sizing: border-box;
      max-width: 52rem;
      margin: 0 auto;
      padding: 32px 24px 48px;
    }

    h1 {
      margin: 0 0 12px;
      font: var(--mat-sys-headline-medium);
    }

    h2 {
      margin: 0 0 12px;
      font: var(--mat-sys-title-large);
    }

    .resumen {
      margin: 0 0 24px;
      font: var(--mat-sys-body-large);
      color: var(--mat-sys-on-surface-variant);
    }

    .aviso {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      margin: 0 0 32px;
      padding: 16px;
      border-radius: var(--mat-sys-corner-medium);
      background-color: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);

      p {
        margin: 0;
      }
    }

    .fila {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      margin: 0 0 12px;
    }

    .correcto mat-icon {
      color: var(--mat-sys-primary);
    }

    .error mat-icon {
      color: var(--mat-sys-error);
    }
  `,
})
export class Inicio {
  private readonly api = inject(EstadoApi);

  protected readonly salud = this.api.salud();
  protected readonly catalogos = this.api.catalogos();

  protected readonly totalFilas = computed(() =>
    this.catalogos.hasValue() ? this.catalogos.value()!.catalogos.reduce((suma, c) => suma + c.filas, 0) : 0,
  );
}

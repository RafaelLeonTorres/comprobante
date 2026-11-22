import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { MatAnchor } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { resaltarXml } from '../../compartido/resaltado-xml';
import { EstadoApi } from '../../core/estado-api';

/**
 * El elemento llamativo del inicio, como el registro en C# del sitio: el arranque de un CFDI 4.0, abreviado. Es solo una
 * ilustración (le faltan atributos y nodos obligatorios) y por eso va oculta a los lectores de pantalla.
 */
const EJEMPLO_XML = `<cfdi:Comprobante Version="4.0"
    Serie="A" Folio="1001"
    Fecha="2026-11-22T10:30:00"
    TipoDeComprobante="I"
    Moneda="MXN" Total="1160.00">
  <cfdi:Emisor Rfc="EKU9003173C9"
      RegimenFiscal="601"/>
  <cfdi:Receptor Rfc="XAXX010101000"
      UsoCFDI="S01"/>
  <cfdi:Conceptos>
    <cfdi:Concepto Cantidad="1"
        Importe="1000.00"/>
  </cfdi:Conceptos>
  <!-- abreviado -->
</cfdi:Comprobante>`;

@Component({
  selector: 'app-inicio',
  imports: [DatePipe, DecimalPipe, MatAnchor, MatIcon, RouterLink],
  template: `
    <section class="portada">
      <div class="presentacion">
        <h1>Comprobante</h1>
        <p class="rol">Generador y validador de XML CFDI 4.0</p>
        <p class="resumen">
          Arma, sella y revisa comprobantes fiscales sin timbrarlos. Calcula importes e impuestos con los
          redondeos del Anexo 20, genera el XML con un certificado de prueba del SAT y revisa cualquier
          XML contra los esquemas y las reglas de validación, con el código de cada error.
        </p>
        <div class="acciones">
          <a matButton="filled" routerLink="/generar">Generar un CFDI</a>
          <a matButton="outlined" routerLink="/validar">Validar un XML</a>
        </div>
      </div>

      <figure class="registro" aria-hidden="true">
        <pre class="xml-resaltado"><code>@for (linea of ejemplo; track $index) {<span class="linea">@for (token of linea; track $index) {<span [class]="token.tipo">{{ token.texto }}</span>}</span>}</code></pre>
      </figure>
    </section>

    <aside class="aviso" role="note">
      <mat-icon svgIcon="advertencia" aria-hidden="true" />
      <p>
        <strong>Sin validez fiscal.</strong> La app no timbra ni envía nada al SAT ni a un PAC: los XML
        que genera no son facturas válidas.
      </p>
    </aside>

    <section class="caminos" aria-labelledby="caminos-titulo">
      <h2 id="caminos-titulo">¿Qué quieres hacer?</h2>
      <ul>
        <li>
          <a routerLink="/generar">
            <mat-icon svgIcon="documento" aria-hidden="true" />
            <span class="titulo">Generar un CFDI</span>
            <span class="detalle">
              Captura emisor, receptor y conceptos, mira los totales mientras escribes y descarga el XML sellado con un
              certificado de prueba.
            </span>
          </a>
        </li>
        <li>
          <a routerLink="/validar">
            <mat-icon svgIcon="correcto" aria-hidden="true" />
            <span class="titulo">Validar un XML</span>
            <span class="detalle">
              Arrastra un archivo o pega el XML: se revisa contra los esquemas del SAT y las reglas de la matriz de errores,
              con el código de cada hallazgo.
            </span>
          </a>
        </li>
        <li>
          <a routerLink="/catalogos">
            <mat-icon svgIcon="buscar" aria-hidden="true" />
            <span class="titulo">Consultar los catálogos</span>
            <span class="detalle">Busca claves y descripciones en los catálogos del SAT cargados en PostgreSQL.</span>
          </a>
        </li>
      </ul>
    </section>

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
      max-width: 72rem;
      margin: 0 auto;
      padding: clamp(2rem, 6vw, 4rem) clamp(1.25rem, 4vw, 2.5rem) 48px;
    }

    h1 {
      margin: 0 0 0.3em;
      font-size: clamp(2.75rem, 8vw, 4.5rem);
      font-weight: 800;
      line-height: 0.95;
      letter-spacing: -0.04em;
    }

    h2 {
      margin: 0 0 12px;
      font: var(--mat-sys-title-large);
    }

    .portada {
      display: grid;
      gap: 2.5rem 3.5rem;
      align-items: center;
      margin: 0 0 40px;

      @media (min-width: 60rem) {
        grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.1fr);
      }
    }

    .rol {
      margin: 0 0 12px;
      font-size: clamp(1.25rem, 2.4vw, 1.5rem);
      font-weight: 700;
    }

    .resumen {
      max-width: 36rem;
      margin: 0 0 24px;
      color: var(--mat-sys-on-surface-variant);
    }

    .acciones {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;

      a {
        --mat-button-filled-container-height: 48px;
        --mat-button-outlined-container-height: 48px;
      }
    }

    /* El elemento llamativo: una tarjeta con borde de tinta y sombra sólida morada, como el registro del sitio. */
    .registro {
      min-width: 0;
      margin: 0 12px 12px 0;
    }

    .registro pre {
      margin: 0;
      padding: clamp(0.9rem, 3vw, 2rem);
      overflow: hidden;
      background-color: var(--sitio-surface);
      border: 2px solid var(--sitio-ink);
      border-radius: var(--mat-sys-corner-large);
      box-shadow: 12px 12px 0 var(--sitio-accent);
      color: var(--sitio-ink);
      font-size: clamp(0.6rem, 2.7vw, 0.98rem);
      line-height: 1.8;
    }

    .registro code {
      font-family: inherit;
    }

    .linea {
      display: block;
      white-space: pre;
    }

    .aviso {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      margin: 0 0 40px;
      padding: 16px;
      border-radius: var(--mat-sys-corner-medium);
      background-color: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);

      p {
        margin: 0;
      }
    }

    .caminos {
      margin: 0 0 40px;

      ul {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr));
        gap: 16px;
        margin: 0;
        padding: 0;
        list-style: none;
      }

      a {
        display: flex;
        flex-direction: column;
        gap: 4px;
        box-sizing: border-box;
        height: 100%;
        padding: 16px;
        border: 1px solid var(--mat-sys-outline-variant);
        border-radius: var(--mat-sys-corner-medium);
        background-color: var(--sitio-surface);
        color: inherit;
        text-decoration: none;

        &:hover {
          border-color: var(--mat-sys-primary);
        }
      }

      mat-icon {
        margin-bottom: 4px;
        color: var(--mat-sys-primary);
      }

      .titulo {
        font: var(--mat-sys-title-medium);
        color: var(--mat-sys-primary);
      }

      .detalle {
        font: var(--mat-sys-body-medium);
        color: var(--mat-sys-on-surface-variant);
      }
    }

    .fila {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      margin: 0 0 12px;
    }

    .correcto mat-icon {
      color: var(--sitio-ok);
    }

    .error mat-icon {
      color: var(--mat-sys-error);
    }
  `,
})
export class Inicio {
  private readonly api = inject(EstadoApi);

  protected readonly ejemplo = resaltarXml(EJEMPLO_XML);
  protected readonly salud = this.api.salud();
  protected readonly catalogos = this.api.catalogos();

  protected readonly totalFilas = computed(() =>
    this.catalogos.hasValue() ? this.catalogos.value()!.catalogos.reduce((suma, c) => suma + c.filas, 0) : 0,
  );
}

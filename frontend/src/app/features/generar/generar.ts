import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, ElementRef, Injector, afterNextRender, computed, effect, inject, signal, viewChild } from '@angular/core';
import { FieldTree, FormField, FormRoot, TreeValidationResult, ValidationError, form } from '@angular/forms/signals';
import { MatButton } from '@angular/material/button';
import { MatButtonToggle, MatButtonToggleGroup } from '@angular/material/button-toggle';
import { MatCheckbox } from '@angular/material/checkbox';
import { MatOption } from '@angular/material/core';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSelect } from '@angular/material/select';

import { BotonPdf } from '../../compartido/boton-pdf';
import { CampoCatalogo } from '../../compartido/campo-catalogo';
import { OPCIONES_DE_CAMPO } from '../../compartido/opciones-de-campo';
import { RevisionXml } from '../../compartido/revision-xml';
import { ApiComprobante } from '../../core/api-comprobante';
import { descargarTexto } from '../../core/descarga';
import { ErrorApi, aErrorApi } from '../../core/errores-api';
import { enfocarArriba } from '../../core/foco';
import { EmisorDePrueba, ResultadoCalculo, ResultadoGeneracion } from '../../core/modelos';
import { conUltimoValor, retrasada } from '../../core/senales';
import { buscarCampo, etiquetaDeRuta, primerMensaje, rutaDeCampo } from './campos';
import { EJEMPLOS, EjemploDeComprobante } from './ejemplos';
import { ConceptoFormulario, FormularioCfdi, aSolicitud, conceptoNuevo, esPublicoEnGeneral, esquemaFormulario, formularioVacio } from './formulario';
import { TarjetaConcepto } from './tarjeta-concepto';
import { EstadoDeTotales, TotalesCalculo } from './totales-calculo';

interface ProblemaDeCampo {
  etiqueta: string;
  mensaje: string;
  campo: FieldTree<unknown>;
}

@Component({
  selector: 'app-generar',
  imports: [
    BotonPdf,
    CampoCatalogo,
    DatePipe,
    FormField,
    FormRoot,
    MatButton,
    MatButtonToggle,
    MatButtonToggleGroup,
    MatCheckbox,
    MatError,
    MatFormField,
    MatHint,
    MatIcon,
    MatInput,
    MatLabel,
    MatOption,
    MatProgressBar,
    MatSelect,
    RevisionXml,
    TarjetaConcepto,
    TotalesCalculo,
  ],
  providers: [OPCIONES_DE_CAMPO],
  template: `
    <h1>Generar un CFDI 4.0</h1>
    <p class="intro">
      Captura los datos, mira los totales mientras escribes y obtén el XML sellado con un certificado de prueba del SAT. El XML
      se revisa con el mismo validador de la pantalla Validar. <strong>No está timbrado: no tiene validez fiscal.</strong>
    </p>

    <section class="ejemplos" aria-labelledby="ejemplos-titulo">
      <h2 id="ejemplos-titulo">Empieza con un ejemplo</h2>
      <div class="botones" role="group" aria-labelledby="ejemplos-titulo">
        @for (ejemplo of ejemplos; track ejemplo.id) {
          <button matButton="outlined" type="button" [attr.title]="ejemplo.ayuda" (click)="cargarEjemplo(ejemplo)">
            <mat-icon svgIcon="ejemplo" aria-hidden="true" />
            {{ ejemplo.titulo }}
          </button>
        }
      </div>
      <p class="aviso-ejemplo" role="status">{{ avisoDeEjemplo() }}</p>
    </section>

    @if (hayProblemas()) {
      <section class="problemas" aria-labelledby="problemas-titulo">
        <h2 id="problemas-titulo" #tituloProblemas tabindex="-1">
          @if (fallo(); as error) {
            {{ error.titulo }}
          } @else {
            Hay datos que corregir
          }
        </h2>
        @if (fallo(); as error) {
          @if (error.detalle) {
            <p>{{ error.detalle }}</p>
          }
        }
        @if (erroresGenerales().length > 0 || problemas().length > 0) {
          <ul>
            @for (mensaje of erroresGenerales(); track $index) {
              <li>{{ mensaje }}</li>
            }
            @for (problema of problemas(); track $index) {
              <li>
                <button type="button" class="enlace" (click)="irAlCampo(problema.campo)">
                  @if (problema.etiqueta) {
                    <strong>{{ problema.etiqueta }}:</strong>
                  }
                  {{ problema.mensaje }}
                </button>
              </li>
            }
          </ul>
        }
      </section>
    }

    <form [formRoot]="formulario" class="disposicion" aria-label="Datos del comprobante">
      <div class="secciones">
        <section class="seccion" aria-labelledby="s-comprobante">
          <h2 id="s-comprobante">Comprobante</h2>
          <div class="rejilla">
            <div class="completo tipo">
              <span id="tipo-etiqueta">Tipo de comprobante</span>
              <mat-button-toggle-group [formField]="formulario.tipoDeComprobante" aria-labelledby="tipo-etiqueta">
                <mat-button-toggle value="I">Ingreso</mat-button-toggle>
                <mat-button-toggle value="E">Egreso</mat-button-toggle>
              </mat-button-toggle-group>
            </div>
            <mat-form-field>
              <mat-label>Serie</mat-label>
              <input matInput type="text" autocomplete="off" [formField]="formulario.serie" />
              <mat-error>{{ primerMensaje(formulario.serie) }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Folio</mat-label>
              <input matInput type="text" autocomplete="off" [formField]="formulario.folio" />
              <mat-error>{{ primerMensaje(formulario.folio) }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Fecha y hora</mat-label>
              <input matInput type="datetime-local" [formField]="formulario.fecha" />
              <mat-hint>Vacía: se usa la hora actual</mat-hint>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Lugar de expedición</mat-label>
              <input matInput type="text" inputmode="numeric" autocomplete="off" [formField]="formulario.lugarExpedicion" />
              <mat-hint>Código postal. Vacío: el del emisor</mat-hint>
              <mat-error>{{ primerMensaje(formulario.lugarExpedicion) }}</mat-error>
            </mat-form-field>
            <app-campo-catalogo [campo]="formulario.formaPago" catalogo="forma-pago" etiqueta="Forma de pago" [tamano]="25" />
            <mat-form-field>
              <mat-label>Método de pago</mat-label>
              <mat-select [formField]="formulario.metodoPago">
                <mat-option value="PUE">PUE · Pago en una sola exhibición</mat-option>
                <mat-option value="PPD">PPD · Pago en parcialidades o diferido</mat-option>
              </mat-select>
              <mat-error>{{ primerMensaje(formulario.metodoPago) }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Condiciones de pago</mat-label>
              <input matInput type="text" autocomplete="off" [formField]="formulario.condicionesDePago" />
            </mat-form-field>
            <app-campo-catalogo [campo]="formulario.moneda" catalogo="moneda" etiqueta="Moneda" ayuda="MXN, USD, EUR..." />
            @if (!formulario.tipoCambio().hidden()) {
              <mat-form-field>
                <mat-label>Tipo de cambio</mat-label>
                <input matInput type="number" inputmode="decimal" step="any" [formField]="formulario.tipoCambio" />
                <mat-hint>Pesos por unidad de la moneda</mat-hint>
                <mat-error>{{ primerMensaje(formulario.tipoCambio) }}</mat-error>
              </mat-form-field>
            }
          </div>
        </section>

        <section class="seccion" aria-labelledby="s-emisor">
          <h2 id="s-emisor">Emisor</h2>
          <p class="nota-seccion">
            Solo se sella con los diez certificados de prueba que publica el SAT. El nombre, el régimen y el código postal salen de
            los datos registrados para cada RFC.
          </p>
          <div class="rejilla">
            <mat-form-field class="completo">
              <mat-label>Emisor de prueba</mat-label>
              <mat-select [formField]="formulario.emisor.rfc">
                @for (emisor of listaDeEmisores(); track emisor.rfc) {
                  <mat-option [value]="emisor.rfc">{{ emisor.rfc }} · {{ emisor.nombre }}</mat-option>
                }
              </mat-select>
              <mat-error>{{ primerMensaje(formulario.emisor.rfc) }}</mat-error>
            </mat-form-field>
          </div>
          @if (emisores.error()) {
            <p class="error-seccion" role="alert">No se pudo cargar la lista de emisores de prueba. Revisa que la API esté corriendo.</p>
          }
          @if (emisorElegido(); as emisor) {
            <dl class="datos">
              <div>
                <dt>Persona</dt>
                <dd>{{ emisor.persona === 'moral' ? 'Moral' : 'Física' }}</dd>
              </div>
              <div>
                <dt>Régimen fiscal</dt>
                <dd>{{ emisor.regimenFiscal }}</dd>
              </div>
              <div>
                <dt>Código postal</dt>
                <dd>{{ emisor.codigoPostal }}</dd>
              </div>
              <div>
                <dt>Certificado</dt>
                <dd class="mono">{{ emisor.noCertificado }}</dd>
              </div>
              <div>
                <dt>Vigente hasta</dt>
                <dd>{{ emisor.validoHasta | date: 'mediumDate' }}</dd>
              </div>
            </dl>
          }
        </section>

        <section class="seccion" aria-labelledby="s-receptor">
          <h2 id="s-receptor">Receptor</h2>
          <div class="rejilla">
            <mat-form-field>
              <mat-label>RFC</mat-label>
              <input matInput type="text" autocomplete="off" spellcheck="false" class="mono" [formField]="formulario.receptor.rfc" />
              <mat-error>{{ primerMensaje(formulario.receptor.rfc) }}</mat-error>
            </mat-form-field>
            <mat-form-field class="doble">
              <mat-label>Nombre o razón social</mat-label>
              <input matInput type="text" autocomplete="off" [formField]="formulario.receptor.nombre" />
              <mat-hint>Sin régimen societario (S.A. de C.V.), como en su constancia</mat-hint>
              <mat-error>{{ primerMensaje(formulario.receptor.nombre) }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Código postal del domicilio fiscal</mat-label>
              <input matInput type="text" inputmode="numeric" autocomplete="off" [formField]="formulario.receptor.domicilioFiscalReceptor" />
              <mat-error>{{ primerMensaje(formulario.receptor.domicilioFiscalReceptor) }}</mat-error>
            </mat-form-field>
            <app-campo-catalogo [campo]="formulario.receptor.regimenFiscalReceptor" catalogo="regimen-fiscal" etiqueta="Régimen fiscal" [tamano]="25" />
            <app-campo-catalogo [campo]="formulario.receptor.usoCfdi" catalogo="uso-cfdi" etiqueta="Uso del CFDI" [tamano]="25" />
            <mat-checkbox class="completo" [formField]="formulario.receptor.extranjero">Receptor extranjero</mat-checkbox>
            @if (formulario.receptor.extranjero().value()) {
              <app-campo-catalogo [campo]="formulario.receptor.residenciaFiscal" catalogo="pais" etiqueta="Residencia fiscal" [minimo]="1" ayuda="Clave de país: USA, CAN..." />
              <mat-form-field>
                <mat-label>Registro de identidad tributaria</mat-label>
                <input matInput type="text" autocomplete="off" [formField]="formulario.receptor.numRegIdTrib" />
              </mat-form-field>
            }
          </div>
        </section>

        @if (esPublico()) {
          <section class="seccion" aria-labelledby="s-global">
            <h2 id="s-global">Información global</h2>
            <p class="nota-seccion">El receptor es el público en general: el comprobante lleva el periodo al que corresponden las ventas.</p>
            <div class="rejilla">
              <app-campo-catalogo [campo]="formulario.informacionGlobal.periodicidad" catalogo="periodicidad" etiqueta="Periodicidad" [tamano]="25" />
              <app-campo-catalogo [campo]="formulario.informacionGlobal.meses" catalogo="meses" etiqueta="Mes o bimestre" [tamano]="25" />
              <mat-form-field>
                <mat-label>Año</mat-label>
                <input matInput type="number" inputmode="numeric" step="1" [formField]="formulario.informacionGlobal.anio" />
                <mat-error>{{ primerMensaje(formulario.informacionGlobal.anio) }}</mat-error>
              </mat-form-field>
            </div>
          </section>
        }

        <section class="seccion" aria-labelledby="s-relacionados">
          <h2 id="s-relacionados">CFDI relacionados</h2>
          <mat-checkbox [formField]="formulario.cfdiRelacionados.activos">
            Este comprobante se relaciona con otros (nota de crédito, sustitución...)
          </mat-checkbox>
          @if (formulario.cfdiRelacionados.activos().value()) {
            <div class="rejilla">
              <app-campo-catalogo [campo]="formulario.cfdiRelacionados.tipoRelacion" catalogo="tipo-relacion" etiqueta="Tipo de relación" [tamano]="25" />
              <mat-form-field class="completo">
                <mat-label>Folios fiscales (UUID)</mat-label>
                <textarea matInput rows="3" spellcheck="false" class="mono" [formField]="formulario.cfdiRelacionados.uuids"></textarea>
                <mat-hint>Uno por renglón</mat-hint>
                <mat-error>{{ primerMensaje(formulario.cfdiRelacionados.uuids) }}</mat-error>
              </mat-form-field>
            </div>
          }
        </section>

        <section class="seccion" aria-labelledby="s-conceptos">
          <h2 id="s-conceptos">Conceptos</h2>
          @for (concepto of formulario.conceptos; track identidad(concepto); let i = $index) {
            <app-tarjeta-concepto
              [concepto]="concepto"
              [numero]="i + 1"
              [calculado]="ultimoCalculo()?.conceptos?.[i]"
              [decimales]="decimales()"
              [puedeQuitar]="modelo().conceptos.length > 1"
              (quitar)="quitarConcepto(i)"
            />
          }
          @if (primerMensaje(formulario.conceptos); as mensaje) {
            <p class="error-seccion" role="alert">{{ mensaje }}</p>
          }
          <button matButton="outlined" type="button" id="agregar-concepto" (click)="agregarConcepto()">
            <mat-icon svgIcon="agregar" aria-hidden="true" />
            Agregar concepto
          </button>
        </section>
      </div>

      <div class="lateral">
        <app-totales-calculo [estado]="estadoDeTotales()" [calculo]="ultimoCalculo()" [error]="errorDeCalculo()" />
        <button matButton="filled" type="submit" class="generar" [disabled]="formulario().submitting()">
          <mat-icon svgIcon="documento" aria-hidden="true" />
          Generar CFDI
        </button>
        @if (formulario().submitting()) {
          <div class="progreso" role="status">
            <mat-progress-bar mode="indeterminate" aria-label="Generando el XML" />
            <p>Generando y sellando…</p>
          </div>
        }
        <p class="pie-lateral">El XML se arma, se sella con el certificado de prueba del emisor y se revisa. Nada se guarda en el servidor.</p>
      </div>
    </form>

    @if (resultado(); as generado) {
      <section class="resultado" aria-labelledby="resultado-titulo">
        <h2 id="resultado-titulo" #tituloResultado tabindex="-1">CFDI generado</h2>
        <p class="resumen-resultado">
          @if (generado.validacion.esValido) {
            El validador no encontró hallazgos.
          } @else {
            El validador encontró hallazgos: revísalos y corrige el formulario.
          }
          Archivo <code>{{ generado.nombreArchivo }}</code>, sellado con el certificado de prueba
          <code>{{ generado.noCertificado }}</code>. Sin timbrar.
        </p>
        <app-revision-xml [xml]="generado.xml" [reporte]="generado.validacion" etiqueta="XML generado">
          <button matButton="filled" type="button" (click)="descargar(generado)">
            <mat-icon svgIcon="descargar" aria-hidden="true" />
            Descargar XML
          </button>
          <app-boton-pdf
            [xml]="generado.xml"
            [habilitado]="generado.validacion.esValido"
            motivo="El PDF se habilita cuando el XML no tiene errores."
          />
        </app-revision-xml>
      </section>
    }
  `,
  styles: `
    :host {
      display: block;
      box-sizing: border-box;
      max-width: 80rem;
      margin: 0 auto;
      padding: 32px 24px 48px;
    }

    h1 {
      margin: 0 0 8px;
      font: var(--mat-sys-headline-medium);
    }

    h2 {
      outline: none;
    }

    .intro {
      max-width: 52rem;
      margin: 0 0 24px;
      color: var(--mat-sys-on-surface-variant);
    }

    .ejemplos {
      margin-bottom: 24px;

      h2 {
        margin: 0 0 8px;
        font: var(--mat-sys-title-medium);
      }
    }

    .botones {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }

    .aviso-ejemplo {
      min-height: 1.5em;
      margin: 8px 0 0;
      color: var(--mat-sys-on-surface-variant);
    }

    .problemas {
      margin-bottom: 24px;
      padding: 16px;
      border-radius: var(--mat-sys-corner-medium);
      background-color: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);

      h2 {
        margin: 0 0 8px;
        font: var(--mat-sys-title-medium);
      }

      p,
      ul {
        margin: 8px 0 0;
      }

      ul {
        padding-inline-start: 20px;
      }
    }

    .enlace {
      padding: 0;
      border: 0;
      background: none;
      color: inherit;
      font: inherit;
      text-align: start;
      text-decoration: underline;
      cursor: pointer;
    }

    .disposicion {
      display: grid;
      gap: 0 24px;
    }

    .tipo {
      display: flex;
      flex-direction: column;
      gap: 4px;
      margin-bottom: 12px;
      font: var(--mat-sys-label-large);

      mat-button-toggle-group {
        align-self: flex-start;
      }
    }

    .rejilla > .doble {
      grid-column: span 2;
    }

    .nota-seccion {
      margin: 0 0 12px;
      color: var(--mat-sys-on-surface-variant);
    }

    .error-seccion {
      margin: 0 0 12px;
      color: var(--mat-sys-error);
    }

    .datos {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
      gap: 8px 16px;
      margin: 0 0 16px;

      dt {
        font: var(--mat-sys-label-medium);
        color: var(--mat-sys-on-surface-variant);
      }

      dd {
        margin: 0;
        overflow-wrap: anywhere;
      }
    }

    .lateral {
      align-self: start;
      padding: 16px;
      border-radius: var(--mat-sys-corner-medium);
      background-color: var(--mat-sys-surface-container);
    }

    .generar {
      width: 100%;
      margin-top: 16px;
    }

    .progreso p,
    .pie-lateral {
      margin: 8px 0 0;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }

    .resultado {
      margin-top: 32px;

      h2 {
        margin: 0 0 8px;
        font: var(--mat-sys-title-large);
      }
    }

    .resumen-resultado {
      max-width: 60rem;
      margin: 0 0 16px;
    }

    @media (min-width: 64rem) {
      .disposicion {
        grid-template-columns: minmax(0, 1fr) 21rem;
      }

      .lateral {
        position: sticky;
        top: 16px;
      }
    }
  `,
})
export class Generar {
  private readonly api = inject(ApiComprobante);
  private readonly injector = inject(Injector);

  protected readonly ejemplos = EJEMPLOS;
  protected readonly primerMensaje = primerMensaje;
  /**
   * Los conceptos se siguen por la identidad de su campo, que se conserva al editarlos, agregarlos o quitarlos. Cargar un
   * ejemplo los reemplaza todos a propósito; con `track concepto` a secas, Angular lo tomaría por un error y avisaría en la
   * consola en cada carga.
   */
  protected readonly identidad = (campo: FieldTree<ConceptoFormulario>): FieldTree<ConceptoFormulario> => campo;

  protected readonly modelo = signal<FormularioCfdi>(formularioVacio());
  protected readonly formulario = form(this.modelo, esquemaFormulario, {
    submission: {
      action: () => this.enviar(),
      onInvalid: () => this.alIntentarConErrores(),
    },
  });

  protected readonly resultado = signal<ResultadoGeneracion | null>(null);
  protected readonly fallo = signal<ErrorApi | null>(null);
  protected readonly erroresGenerales = signal<readonly string[]>([]);
  protected readonly avisoDeEjemplo = signal('');
  /** Hasta el primer intento de generar no se lista lo que falta: un formulario recién abierto no empieza regañando. */
  private readonly intentado = signal(false);

  protected readonly tituloProblemas = viewChild<ElementRef<HTMLElement>>('tituloProblemas');
  protected readonly tituloResultado = viewChild<ElementRef<HTMLElement>>('tituloResultado');
  private readonly pidioFocoEnProblemas = signal(0);

  // Emisores de prueba ---------------------------------------------------------------------------------------------------
  protected readonly emisores = httpResource<EmisorDePrueba[]>(() => '/api/emisores-prueba');
  protected readonly listaDeEmisores = computed(() => (this.emisores.hasValue() ? this.emisores.value() : []));
  protected readonly emisorElegido = computed(() => this.listaDeEmisores().find((e) => e.rfc === this.modelo().emisor.rfc));

  protected readonly esPublico = computed(() => esPublicoEnGeneral(this.modelo().receptor));

  // Totales en vivo -------------------------------------------------------------------------------------------------------
  private readonly solicitudRetrasada = retrasada(computed(() => aSolicitud(this.modelo())), 400);
  private readonly calculable = computed(() =>
    (this.solicitudRetrasada().conceptos ?? []).some((c) => c.cantidad > 0 && c.valorUnitario > 0),
  );
  private readonly calculo = httpResource<ResultadoCalculo>(() =>
    this.calculable() ? { url: '/api/comprobantes/calculo', method: 'POST', body: this.solicitudRetrasada() } : undefined,
  );

  protected readonly ultimoCalculo = conUltimoValor(() => (this.calculo.hasValue() ? this.calculo.value() : undefined));
  protected readonly decimales = computed(() => this.ultimoCalculo()?.decimalesMoneda ?? 2);
  protected readonly errorDeCalculo = computed(() => (this.calculo.error() ? aErrorApi(this.calculo.error()) : null));
  protected readonly estadoDeTotales = computed<EstadoDeTotales>(() => {
    if (!this.calculable()) {
      return 'vacio';
    }

    if (this.calculo.error()) {
      return 'error';
    }

    return this.calculo.isLoading() ? 'calculando' : 'listo';
  });

  // Lo que hay que corregir ------------------------------------------------------------------------------------------------
  protected readonly problemas = computed<ProblemaDeCampo[]>(() => {
    if (!this.intentado()) {
      return [];
    }

    return this.formulario()
      .errorSummary()
      .map((error) => ({
        etiqueta: etiquetaDeRuta(rutaDeCampo(this.formulario, this.modelo(), error.fieldTree) ?? ''),
        mensaje: error.message ?? 'Dato no válido.',
        campo: error.fieldTree as FieldTree<unknown>,
      }));
  });
  protected readonly hayProblemas = computed(
    () => this.fallo() !== null || this.erroresGenerales().length > 0 || this.problemas().length > 0,
  );

  constructor() {
    // Con un fallo o con datos por corregir, el foco pasa al aviso: lo oye quien usa lector de pantalla y el teclado sigue desde ahí.
    effect(() => {
      if (this.pidioFocoEnProblemas() > 0) {
        afterNextRender(() => enfocarArriba(this.tituloProblemas()?.nativeElement), { injector: this.injector });
      }
    });

    effect(() => {
      if (this.resultado()) {
        afterNextRender(() => enfocarArriba(this.tituloResultado()?.nativeElement), { injector: this.injector });
      }
    });

    // Al escribir el RFC y el nombre del público en general, la información global aparece ya con el mes en curso.
    effect(() => {
      const global = this.formulario.informacionGlobal;
      if (this.esPublico() && global.anio().value() === null) {
        const hoy = new Date();
        global.periodicidad().value.set('01');
        global.meses().value.set(String(hoy.getMonth() + 1).padStart(2, '0'));
        global.anio().value.set(hoy.getFullYear());
      }
    });
  }

  protected cargarEjemplo(ejemplo: EjemploDeComprobante): void {
    this.formulario().reset(ejemplo.crear());
    this.intentado.set(false);
    this.resultado.set(null);
    this.fallo.set(null);
    this.erroresGenerales.set([]);
    this.avisoDeEjemplo.set(`Se cargó «${ejemplo.titulo}». Revisa los datos y genera el XML.`);
  }

  protected agregarConcepto(): void {
    this.formulario.conceptos().value.update((lista) => [...lista, conceptoNuevo()]);
    const indice = this.modelo().conceptos.length - 1;
    afterNextRender(() => this.formulario.conceptos[indice]?.claveProdServ().focusBoundControl(), { injector: this.injector });
  }

  protected quitarConcepto(indice: number): void {
    this.formulario.conceptos().value.update((lista) => lista.filter((_, posicion) => posicion !== indice));
    afterNextRender(() => document.getElementById('agregar-concepto')?.focus(), { injector: this.injector });
  }

  protected irAlCampo(campo: FieldTree<unknown>): void {
    if (campo().formFieldBindings().length > 0) {
      campo().focusBoundControl();
    } else {
      document.getElementById('agregar-concepto')?.focus();
    }
  }

  protected descargar(generado: ResultadoGeneracion): void {
    descargarTexto(generado.nombreArchivo, generado.xml);
  }

  private alIntentarConErrores(): void {
    this.intentado.set(true);
    this.pidioFocoEnProblemas.update((veces) => veces + 1);
  }

  /** La acción del formulario: pide el XML a la API y devuelve los errores de campo que esta señale. */
  private async enviar(): Promise<TreeValidationResult> {
    this.intentado.set(true);
    this.fallo.set(null);
    this.erroresGenerales.set([]);
    this.resultado.set(null);

    try {
      this.resultado.set(await this.api.generar(aSolicitud(this.modelo())));
      return undefined;
    } catch (error) {
      return this.alFallar(aErrorApi(error));
    }
  }

  /**
   * Un 400 trae los errores por campo (<c>conceptos[0].cantidad</c>): se ponen en su campo. Lo que no corresponda a un campo
   * (o cualquier otro fallo: sin conexión, un error del servidor) sale en el aviso de arriba.
   */
  private alFallar(error: ErrorApi): ValidationError.WithOptionalFieldTree[] {
    const deCampo: ValidationError.WithOptionalFieldTree[] = [];
    const generales: string[] = [];

    if (error.estado === 400) {
      for (const [ruta, mensajes] of Object.entries(error.campos)) {
        const campo = buscarCampo(this.formulario, ruta);
        for (const mensaje of mensajes) {
          if (campo) {
            deCampo.push({ kind: 'servidor', fieldTree: campo, message: mensaje });
          } else {
            generales.push(mensaje);
          }
        }
      }
    }

    if (deCampo.length === 0 && generales.length === 0) {
      this.fallo.set(error);
    }

    this.erroresGenerales.set(generales);
    this.pidioFocoEnProblemas.update((veces) => veces + 1);
    return deCampo;
  }
}

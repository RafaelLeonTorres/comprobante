import { Component, ElementRef, Injector, afterNextRender, effect, inject, signal, viewChild } from '@angular/core';
import { FormField, FormRoot, form, maxLength, required } from '@angular/forms/signals';
import { MatButton } from '@angular/material/button';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';

import { BotonPdf } from '../../compartido/boton-pdf';
import { OPCIONES_DE_CAMPO } from '../../compartido/opciones-de-campo';
import { RevisionXml } from '../../compartido/revision-xml';
import { ApiComprobante } from '../../core/api-comprobante';
import { ErrorApi, aErrorApi } from '../../core/errores-api';
import { enfocarArriba } from '../../core/foco';
import { ReporteValidacion } from '../../core/modelos';
import { CoberturaReglas } from './cobertura-reglas';

/** Lo mismo que acepta la API (y nginx): un CFDI normal ocupa unos cuantos KB. */
export const TAMANO_MAXIMO = 2 * 1024 * 1024;

const EJEMPLOS = [
  { archivo: 'ingreso-valido.xml', titulo: 'Un CFDI sin errores', ayuda: 'Ingreso con IVA, retenciones y tres conceptos, sellado con un certificado de prueba.' },
  { archivo: 'ingreso-con-errores.xml', titulo: 'Un CFDI con errores', ayuda: 'El mismo, con el total alterado, un régimen, un uso y una unidad que no existen.' },
] as const;

interface XmlEnRevision {
  readonly nombre: string;
  readonly xml: string;
  readonly reporte: ReporteValidacion;
}

/** El archivo como texto. Un CFDI es UTF-8; el navegador quita el BOM si lo trae. */
async function leerTexto(archivo: File): Promise<string> {
  return archivo.text();
}

@Component({
  selector: 'app-validar',
  imports: [BotonPdf, CoberturaReglas, FormField, FormRoot, MatButton, MatError, MatFormField, MatIcon, MatInput, MatLabel, MatProgressBar, RevisionXml],
  providers: [OPCIONES_DE_CAMPO],
  template: `
    <h1>Validar un XML de CFDI 4.0</h1>
    <p class="intro">
      Se revisa contra el esquema del SAT, las reglas de su matriz de errores y el sello digital, y cada hallazgo trae su código.
      El XML no se guarda: se lee, se revisa y se descarta.
    </p>

    <section class="entrada" aria-labelledby="entrada-titulo">
      <h2 id="entrada-titulo">Tu XML</h2>

      <div
        class="zona"
        role="group"
        aria-label="Zona para soltar un archivo XML"
        [class.arrastrando]="arrastrando()"
        (dragover)="alArrastrar($event)"
        (dragleave)="arrastrando.set(false)"
        (drop)="alSoltar($event)"
      >
        <mat-icon svgIcon="subir" aria-hidden="true" />
        <p class="grande">Arrastra aquí un archivo XML</p>
        <p class="chico">o</p>
        <button matButton="filled" type="button" (click)="selector.click()">Elegir archivo</button>
        <input #selector class="oculto" type="file" accept=".xml,text/xml,application/xml" tabindex="-1" aria-hidden="true" (change)="alElegir($event)" />
        <p class="chico">Hasta 2 MB. El archivo se envía a la API solo para revisarlo.</p>
      </div>

      <details class="pegar">
        <summary>O pega el XML</summary>
        <form [formRoot]="pegado" class="formulario-pegar">
          <mat-form-field class="ancho">
            <mat-label>XML a validar</mat-label>
            <textarea matInput rows="8" spellcheck="false" autocomplete="off" class="mono" [formField]="pegado.xml"></textarea>
            @if (pegado.xml().touched() && pegado.xml().invalid()) {
              <mat-error>{{ pegado.xml().errors()[0].message }}</mat-error>
            }
          </mat-form-field>
          <button matButton="filled" type="submit" [disabled]="validando()">Validar</button>
        </form>
      </details>

      <div class="ejemplos">
        <p id="ejemplos-titulo">¿Sin un XML a la mano? Prueba con uno de ejemplo:</p>
        <div class="botones" role="group" aria-labelledby="ejemplos-titulo">
          @for (ejemplo of ejemplos; track ejemplo.archivo) {
            <button matButton="outlined" type="button" [disabled]="validando()" [attr.title]="ejemplo.ayuda" (click)="probarEjemplo(ejemplo.archivo, ejemplo.titulo)">
              <mat-icon svgIcon="ejemplo" aria-hidden="true" />
              {{ ejemplo.titulo }}
            </button>
          }
        </div>
      </div>
    </section>

    @if (validando()) {
      <div class="progreso" role="status">
        <mat-progress-bar mode="indeterminate" aria-label="Validando el XML" />
        <p>Validando…</p>
      </div>
    }

    @if (fallo(); as error) {
      <div class="falla" role="alert">
        <mat-icon svgIcon="error" aria-hidden="true" />
        <p>
          <strong>{{ error.titulo }}.</strong>
          @if (error.detalle) {
            {{ error.detalle }}
          }
        </p>
      </div>
    }

    @if (revision(); as actual) {
      <section class="resultado" aria-labelledby="resultado-titulo">
        <h2 id="resultado-titulo" #titulo tabindex="-1">Resultado: {{ actual.nombre }}</h2>
        <app-revision-xml [xml]="actual.xml" [reporte]="actual.reporte">
          <app-boton-pdf
            [xml]="actual.xml"
            [habilitado]="actual.reporte.esValido"
            motivo="El PDF se habilita cuando el XML no tiene errores."
          />
        </app-revision-xml>
      </section>
    }

    <section class="cobertura" aria-labelledby="cobertura-titulo">
      <h2 id="cobertura-titulo">Qué revisa este validador</h2>
      <p>
        Sin hallazgos significa que el XML cumple lo que esta app revisa, no que el SAT lo vaya a timbrar: no comprueba, por ejemplo,
        que el RFC del receptor exista. Estas son las reglas de la matriz de errores y lo que se hace con cada una.
      </p>
      <app-cobertura-reglas />
    </section>
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
      margin: 0 0 12px;
      font: var(--mat-sys-title-large);
      outline: none;
    }

    .intro {
      max-width: 52rem;
      margin: 0 0 24px;
      color: var(--mat-sys-on-surface-variant);
    }

    .zona {
      display: flex;
      flex-direction: column;
      gap: 4px;
      align-items: center;
      padding: 32px 16px;
      border: 2px dashed var(--mat-sys-outline);
      border-radius: var(--mat-sys-corner-large);
      background-color: var(--mat-sys-surface-container-low);
      text-align: center;

      &.arrastrando {
        border-color: var(--mat-sys-primary);
        background-color: var(--mat-sys-primary-container);
      }

      mat-icon {
        width: 40px;
        height: 40px;
        margin-bottom: 4px;
        color: var(--mat-sys-primary);
      }

      p {
        margin: 0;
      }

      .grande {
        font: var(--mat-sys-title-medium);
      }

      .chico {
        font: var(--mat-sys-body-small);
        color: var(--mat-sys-on-surface-variant);
      }
    }

    .oculto {
      display: none;
    }

    .pegar,
    .ejemplos {
      margin-top: 16px;
    }

    .pegar summary {
      cursor: pointer;
      font: var(--mat-sys-title-small);
    }

    .formulario-pegar {
      display: flex;
      flex-direction: column;
      gap: 8px;
      align-items: flex-start;
      margin-top: 12px;
    }

    .ancho {
      width: 100%;
    }

    .ejemplos p {
      margin: 0 0 8px;
    }

    .botones {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }

    .progreso {
      margin-top: 24px;

      p {
        margin: 8px 0 0;
      }
    }

    .falla {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      margin-top: 24px;
      padding: 16px;
      border-radius: var(--mat-sys-corner-medium);
      background-color: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);

      p {
        margin: 0;
      }
    }

    .resultado {
      margin-top: 32px;
    }

    .cobertura {
      margin-top: 48px;

      > p {
        max-width: 52rem;
        margin: 0 0 16px;
        color: var(--mat-sys-on-surface-variant);
      }
    }
  `,
})
export class Validar {
  private readonly api = inject(ApiComprobante);
  private readonly injector = inject(Injector);

  protected readonly ejemplos = EJEMPLOS;
  protected readonly arrastrando = signal(false);
  protected readonly validando = signal(false);
  protected readonly fallo = signal<ErrorApi | null>(null);
  protected readonly revision = signal<XmlEnRevision | null>(null);

  protected readonly titulo = viewChild<ElementRef<HTMLElement>>('titulo');

  /** El XML pegado: un formulario de un solo campo que se envía con Enter en el botón o con el teclado. */
  protected readonly pegado = form(
    signal({ xml: '' }),
    (ruta) => {
      required(ruta.xml, { message: 'Pega el XML que quieres validar.' });
      maxLength(ruta.xml, TAMANO_MAXIMO, { message: 'El XML pesa más de 2 MB.' });
    },
    {
      submission: {
        action: async (campo) => {
          await this.validarTexto(campo.xml().value(), 'XML pegado');
          return undefined;
        },
      },
    },
  );

  constructor() {
    // Al llegar un resultado, el foco pasa a su título: quien usa lector de pantalla lo oye y el teclado sigue desde ahí.
    effect(() => {
      if (this.revision()) {
        afterNextRender(() => enfocarArriba(this.titulo()?.nativeElement), { injector: this.injector });
      }
    });
  }

  protected alArrastrar(evento: DragEvent): void {
    evento.preventDefault();
    this.arrastrando.set(true);
  }

  protected alSoltar(evento: DragEvent): void {
    evento.preventDefault();
    this.arrastrando.set(false);
    const archivo = evento.dataTransfer?.files?.[0];
    if (archivo) {
      void this.validarArchivo(archivo);
    }
  }

  protected alElegir(evento: Event): void {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    // Se limpia para poder elegir el mismo archivo otra vez después de corregirlo.
    entrada.value = '';
    if (archivo) {
      void this.validarArchivo(archivo);
    }
  }

  protected async probarEjemplo(archivo: string, titulo: string): Promise<void> {
    this.fallo.set(null);
    try {
      await this.validarTexto(await this.api.ejemplo(archivo), titulo);
    } catch (error) {
      this.fallo.set(aErrorApi(error));
    }
  }

  private async validarArchivo(archivo: File): Promise<void> {
    if (archivo.size > TAMANO_MAXIMO) {
      this.revision.set(null);
      this.fallo.set(new ErrorApi(413, 'El archivo es demasiado grande', `${archivo.name} pesa más de 2 MB: un CFDI normal ocupa unos cuantos KB.`));
      return;
    }

    await this.validarTexto(await leerTexto(archivo), archivo.name);
  }

  private async validarTexto(xml: string, nombre: string): Promise<void> {
    this.fallo.set(null);
    this.validando.set(true);
    try {
      const reporte = await this.api.validar(xml);
      this.revision.set({ nombre, xml, reporte });
    } catch (error) {
      this.revision.set(null);
      this.fallo.set(aErrorApi(error));
    } finally {
      this.validando.set(false);
    }
  }
}

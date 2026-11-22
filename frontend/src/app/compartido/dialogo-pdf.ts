import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatCheckbox } from '@angular/material/checkbox';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';

import { ApiComprobante, LIMITES_DEL_PDF } from '../core/api-comprobante';
import { descargarArchivo } from '../core/descarga';
import { ErrorApi, aErrorApi } from '../core/errores-api';
import { PreferenciasDelPdf } from '../core/preferencias-del-pdf';

/** Lo que recibe el diálogo: el XML del que se dibuja el PDF. */
export interface DatosDelDialogoPdf {
  readonly xml: string;
}

/** Lo que devuelve al cerrarse tras descargar: el nombre del archivo. Cancelar no devuelve nada. */
export interface ResultadoDelDialogoPdf {
  readonly nombre: string;
}

/**
 * Qué decirle a quien eligió un archivo que no sirve de logo; nulo si sirve. Solo revisa lo que se ve sin abrirlo (el tipo y el
 * peso): el tamaño en píxeles y el contenido de la imagen los revisa la API, y su respuesta sale junto al logo.
 */
export function errorDelLogo(archivo: File): string | null {
  const esImagen =
    archivo.type === 'image/png' ||
    archivo.type === 'image/jpeg' ||
    (archivo.type === '' && /\.(?:png|jpe?g)$/i.test(archivo.name));
  if (!esImagen) {
    return 'El logo debe ser una imagen PNG o JPEG.';
  }

  if (archivo.size === 0) {
    return 'El archivo del logo está vacío.';
  }

  if (archivo.size > LIMITES_DEL_PDF.logo) {
    return `El logo pesa ${Math.ceil(archivo.size / 1024)} KB; el máximo es ${LIMITES_DEL_PDF.logo / 1024} KB.`;
  }

  return null;
}

/** La imagen como URL de datos: la política de seguridad de la app deja ver imágenes <c>data:</c> pero no <c>blob:</c>. */
function leerComoUrl(archivo: Blob): Promise<string> {
  return new Promise((resolver, rechazar) => {
    const lector = new FileReader();
    lector.onload = () => resolver(String(lector.result));
    lector.onerror = () => rechazar(lector.error);
    lector.readAsDataURL(archivo);
  });
}

/**
 * Las opciones del PDF de un CFDI (el logo y si lleva la cadena original y los sellos) y su descarga. Si la API rechaza algo,
 * el diálogo sigue abierto y lo explica junto al campo que lo causó.
 */
@Component({
  selector: 'app-dialogo-pdf',
  imports: [MatButton, MatCheckbox, MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle, MatIcon, MatProgressBar],
  template: `
    <h2 mat-dialog-title>Descargar PDF</h2>
    <mat-dialog-content>
      <p class="intro">
        El PDF es la representación impresa de este CFDI. Si el XML no está timbrado, como los que arma esta app, el PDF lo marca
        como comprobante de prueba y sin validez fiscal.
      </p>

      <fieldset class="logo">
        <legend>Logo</legend>
        <p id="ayuda-logo" class="ayuda">
          Opcional. PNG o JPEG de hasta 200 KB y 2,000 × 2,000 píxeles; sin él se usa el de la app. Se envía solo para dibujar el
          PDF y no se guarda.
        </p>
        <div class="elegir">
          @if (vistaPrevia(); as vista) {
            <img class="vista" [src]="vista" alt="Vista previa del logo elegido" />
          }
          <button #elegir matButton="outlined" type="button" aria-describedby="ayuda-logo" (click)="selector.click()">
            <mat-icon svgIcon="subir" aria-hidden="true" />
            {{ logo() ? 'Cambiar logo' : 'Elegir logo' }}
          </button>
          @if (logo(); as elegido) {
            <span class="nombre">{{ elegido.name }}</span>
            <button matButton type="button" (click)="quitarLogo()">Quitar logo</button>
          }
          <input
            #selector
            class="oculto"
            type="file"
            accept="image/png,image/jpeg"
            tabindex="-1"
            aria-hidden="true"
            (change)="alElegir($event)"
          />
        </div>
        @if (errorDeLogo(); as mensaje) {
          <p class="error" role="alert">
            <mat-icon svgIcon="error" aria-hidden="true" />
            <span>{{ mensaje }}</span>
          </p>
        }
      </fieldset>

      <mat-checkbox [checked]="incluirCadenaYSellos()" (change)="incluirCadenaYSellos.set($event.checked)">
        Incluir la cadena original y los sellos
      </mat-checkbox>
      <p class="ayuda">
        Son lo que permite revisar el sello del comprobante y hacen el PDF más largo. Sin ellos queda el código QR, si el CFDI
        está timbrado.
      </p>

      @if (fallo(); as error) {
        <div class="error" role="alert">
          <mat-icon svgIcon="error" aria-hidden="true" />
          <p>
            <strong>{{ error.titulo }}.</strong>
            @if (error.detalle) {
              {{ error.detalle }}
            }
          </p>
        </div>
      }

      <div class="progreso">
        @if (generando()) {
          <mat-progress-bar mode="indeterminate" aria-label="Dibujando el PDF" />
        }
        <p role="status">{{ generando() ? 'Dibujando el PDF…' : '' }}</p>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" mat-dialog-close disabledInteractive [disabled]="generando()">Cancelar</button>
      <button matButton="filled" type="button" disabledInteractive [disabled]="generando()" (click)="descargar()">
        <mat-icon svgIcon="descargar" aria-hidden="true" />
        Descargar PDF
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .intro {
      margin: 0 0 16px;
    }

    fieldset {
      margin: 0 0 16px;
      padding: 8px 16px 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
    }

    legend {
      padding: 0 4px;
      font: var(--mat-sys-title-small);
    }

    .ayuda {
      margin: 4px 0 12px;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }

    fieldset .ayuda {
      margin-top: 0;
    }

    .elegir {
      display: flex;
      flex-wrap: wrap;
      gap: 8px 12px;
      align-items: center;
    }

    /* Fondo blanco: es el de la hoja, y un logo con fondo transparente se ve como va a quedar. */
    .vista {
      box-sizing: border-box;
      max-width: 8rem;
      max-height: 3rem;
      padding: 2px;
      object-fit: contain;
      background: #fff;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 4px;
    }

    .nombre {
      font: var(--mat-sys-body-medium);
      overflow-wrap: anywhere;
    }

    .error {
      display: flex;
      gap: 8px;
      align-items: flex-start;
      margin: 8px 0 0;
      font: var(--mat-sys-body-medium);
      color: var(--mat-sys-error);

      p {
        margin: 0;
      }

      mat-icon {
        flex: none;
      }
    }

    .progreso {
      margin-top: 12px;

      p {
        margin: 8px 0 0;
        font: var(--mat-sys-body-medium);
      }

      /* La región de estado siempre está en la página (así se anuncia lo que escribe), pero vacía no ocupa lugar. */
      p:empty {
        margin: 0;
      }
    }

    .oculto {
      display: none;
    }
  `,
})
export class DialogoPdf {
  private readonly api = inject(ApiComprobante);
  private readonly datos = inject<DatosDelDialogoPdf>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<DialogoPdf, ResultadoDelDialogoPdf>>(MatDialogRef);
  private readonly preferencias = inject(PreferenciasDelPdf);
  // Con read: sin él, un #elegir sobre un botón de Material daría el MatButton, no el elemento.
  private readonly botonElegir = viewChild('elegir', { read: ElementRef });

  protected readonly logo = signal<File | null>(this.preferencias.logo());
  protected readonly vistaPrevia = signal<string | null>(null);
  protected readonly incluirCadenaYSellos = signal(this.preferencias.incluirCadenaYSellos());
  protected readonly errorDeLogo = signal<string | null>(null);
  protected readonly fallo = signal<ErrorApi | null>(null);
  protected readonly generando = signal(false);

  constructor() {
    const recordado = this.logo();
    if (recordado) {
      void this.mostrar(recordado);
    }
  }

  protected async alElegir(evento: Event): Promise<void> {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    entrada.value = ''; // para poder elegir otra vez el mismo archivo
    if (!archivo) {
      return;
    }

    const error = errorDelLogo(archivo);
    this.errorDeLogo.set(error);
    if (error) {
      return;
    }

    this.logo.set(archivo);
    await this.mostrar(archivo);
  }

  protected quitarLogo(): void {
    this.logo.set(null);
    this.vistaPrevia.set(null);
    this.errorDeLogo.set(null);
    this.botonElegir()?.nativeElement.focus(); // el botón de quitar desaparece con el logo: el foco pasa al de elegir
  }

  protected async descargar(): Promise<void> {
    if (this.generando()) {
      return; // con disabledInteractive el botón sigue recibiendo el foco, pero no debe lanzar otra solicitud
    }

    this.fallo.set(null);
    this.errorDeLogo.set(null);
    this.generando.set(true);
    this.ref.disableClose = true;

    try {
      const pdf = await this.api.pdf(this.datos.xml, { logo: this.logo(), incluirCadenaYSellos: this.incluirCadenaYSellos() });
      this.preferencias.logo.set(this.logo());
      this.preferencias.incluirCadenaYSellos.set(this.incluirCadenaYSellos());
      descargarArchivo(pdf.nombre, pdf.archivo);
      this.ref.close({ nombre: pdf.nombre });
    } catch (excepcion) {
      this.alFallar(aErrorApi(excepcion));
    } finally {
      this.generando.set(false);
      this.ref.disableClose = false;
    }
  }

  /** Lo del logo sale junto al logo; lo del XML y lo demás, en el aviso del diálogo. */
  private alFallar(error: ErrorApi): void {
    const deLogo = error.estado === 400 ? error.campos['logo']?.[0] : undefined;
    const deXml = error.estado === 400 ? error.campos['xml']?.[0] : undefined;

    if (deLogo) {
      this.errorDeLogo.set(deLogo);
    }

    if (deXml) {
      this.fallo.set(new ErrorApi(400, 'El XML no se pudo dibujar', deXml));
    } else if (!deLogo) {
      this.fallo.set(error);
    }
  }

  private async mostrar(archivo: File): Promise<void> {
    try {
      this.vistaPrevia.set(await leerComoUrl(archivo));
    } catch {
      this.vistaPrevia.set(null); // sin vista previa: el nombre del archivo sigue a la vista
    }
  }
}

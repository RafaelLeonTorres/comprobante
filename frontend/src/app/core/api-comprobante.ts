import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';

import { nombreDeDescarga } from './descarga';
import { aErrorApi, conCuerpoLegible } from './errores-api';
import { ReporteValidacion, ResultadoGeneracion, SolicitudCfdi } from './modelos';

/** Lo que acepta la API para dibujar un PDF; son los mismos límites que revisa el servidor. */
export const LIMITES_DEL_PDF = {
  /** El XML pesa hasta 1 MB. */
  xml: 1024 * 1024,
  /** El logo pesa hasta 200 KB (y mide hasta 2,000 × 2,000 px, que solo revisa el servidor). */
  logo: 200 * 1024,
} as const;

/** Lo que se elige para el PDF: un logo opcional y si lleva la cadena original y los sellos. */
export interface OpcionesDelPdf {
  /** Un PNG o JPEG para el encabezado; sin él, el PDF lleva el logo de la app. */
  readonly logo?: File | null;
  /** Con false se omiten la cadena original y los sellos. */
  readonly incluirCadenaYSellos: boolean;
}

/** El PDF que dibujó la API y el nombre con el que se propone descargarlo. */
export interface PdfGenerado {
  readonly archivo: Blob;
  readonly nombre: string;
}

/**
 * Las operaciones de la API que se hacen con un clic (generar, validar y dibujar el PDF). Lo que se lee al abrir una
 * pantalla (catálogos, emisores de prueba, reglas, totales en vivo) va con <c>httpResource</c> en cada componente. Los
 * fallos salen como <c>ErrorApi</c>, ya explicados en español.
 */
@Injectable({ providedIn: 'root' })
export class ApiComprobante {
  private readonly http = inject(HttpClient);

  /** Arma y sella un CFDI con el emisor de prueba de la solicitud. */
  generar(solicitud: SolicitudCfdi): Promise<ResultadoGeneracion> {
    return this.pedir(this.http.post<ResultadoGeneracion>('/api/comprobantes', solicitud));
  }

  /** Valida un XML tal como llega: un XML con errores es una respuesta normal con sus hallazgos, no un fallo. */
  validar(xml: string): Promise<ReporteValidacion> {
    return this.pedir(
      this.http.post<ReporteValidacion>('/api/validaciones', xml, {
        headers: { 'Content-Type': 'application/xml; charset=utf-8' },
      }),
    );
  }

  /** Dibuja la representación impresa de un CFDI en PDF. El XML y el logo viajan en un formulario y no se guardan. */
  pdf(xml: string, opciones: OpcionesDelPdf): Promise<PdfGenerado> {
    const formulario = new FormData();
    formulario.append('xml', new Blob([xml], { type: 'application/xml' }), 'comprobante.xml');
    if (opciones.logo) {
      formulario.append('logo', opciones.logo, opciones.logo.name);
    }
    formulario.append('incluirCadenaYSellos', String(opciones.incluirCadenaYSellos));

    return this.pedirPdf(
      this.http.post('/api/representaciones/pdf', formulario, { observe: 'response', responseType: 'blob' }),
    );
  }

  /** Un XML de ejemplo que viaja con la app (<c>public/ejemplos</c>). */
  ejemplo(nombre: string): Promise<string> {
    return this.pedir(this.http.get(`/ejemplos/${nombre}`, { responseType: 'text' }));
  }

  private async pedirPdf(peticion: Observable<HttpResponse<Blob>>): Promise<PdfGenerado> {
    try {
      const respuesta = await firstValueFrom(peticion);
      return {
        archivo: respuesta.body ?? new Blob([], { type: 'application/pdf' }),
        nombre: nombreDeDescarga(respuesta.headers.get('Content-Disposition')),
      };
    } catch (error) {
      throw aErrorApi(await conCuerpoLegible(error));
    }
  }

  private async pedir<T>(peticion: Observable<T>): Promise<T> {
    try {
      return await firstValueFrom(peticion);
    } catch (error) {
      throw aErrorApi(error);
    }
  }
}

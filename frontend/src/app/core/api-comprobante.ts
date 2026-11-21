import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';

import { aErrorApi } from './errores-api';
import { ReporteValidacion, ResultadoGeneracion, SolicitudCfdi } from './modelos';

/**
 * Las operaciones de la API que se hacen con un clic (generar y validar). Lo que se lee al abrir una pantalla (catálogos,
 * emisores de prueba, reglas, totales en vivo) va con <c>httpResource</c> en cada componente. Los fallos salen como
 * <c>ErrorApi</c>, ya explicados en español.
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

  /** Un XML de ejemplo que viaja con la app (<c>public/ejemplos</c>). */
  ejemplo(nombre: string): Promise<string> {
    return this.pedir(this.http.get(`/ejemplos/${nombre}`, { responseType: 'text' }));
  }

  private async pedir<T>(peticion: Observable<T>): Promise<T> {
    try {
      return await firstValueFrom(peticion);
    } catch (error) {
      throw aErrorApi(error);
    }
  }
}

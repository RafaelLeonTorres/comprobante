import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { aErrorApi } from './errores-api';
import { FilaCatalogo, PaginaCatalogo } from './modelos';

export interface OpcionesDeBusqueda {
  /** Texto que se busca en la clave y en la descripción (sin distinguir mayúsculas). */
  texto?: string;
  pagina?: number;
  tamano?: number;
}

/** El texto que describe una fila: casi todos los catálogos lo llaman <c>descripcion</c>; c_ClaveUnidad y c_Estado, <c>nombre</c>. */
export function descripcionDe(fila: FilaCatalogo): string {
  return String(fila['descripcion'] ?? fila['nombre'] ?? '');
}

const MAXIMO_EN_MEMORIA = 500;

/**
 * Consulta de catálogos para los campos con autocompletado y para la pantalla de catálogos. Las consultas repetidas (la misma
 * unidad en tres conceptos, la misma clave tecleada otra vez) se responden de memoria; un fallo no se recuerda.
 */
@Injectable({ providedIn: 'root' })
export class ServicioCatalogos {
  private readonly http = inject(HttpClient);
  private readonly consultas = new Map<string, Promise<PaginaCatalogo>>();

  buscar(catalogo: string, { texto = '', pagina = 1, tamano = 10 }: OpcionesDeBusqueda = {}): Promise<PaginaCatalogo> {
    const q = texto.trim();
    const llave = `${catalogo}|${q.toLowerCase()}|${pagina}|${tamano}`;

    let consulta = this.consultas.get(llave);
    if (!consulta) {
      const params: Record<string, string | number> = { pagina, tamano };
      if (q) {
        params['q'] = q;
      }

      consulta = firstValueFrom(this.http.get<PaginaCatalogo>(`/api/catalogos/${catalogo}`, { params })).catch((error: unknown) => {
        this.consultas.delete(llave);
        throw aErrorApi(error);
      });
      if (this.consultas.size >= MAXIMO_EN_MEMORIA) {
        this.consultas.delete(this.consultas.keys().next().value!);
      }

      this.consultas.set(llave, consulta);
    }

    return consulta;
  }

  /**
   * La fila de una clave, o <c>null</c> si no está en el catálogo. Busca con el mismo texto y tamaño que el autocompletado, así
   * que lo que ese campo ya preguntó no se vuelve a pedir.
   */
  async describir(catalogo: string, clave: string, tamano = 10): Promise<FilaCatalogo | null> {
    const pagina = await this.buscar(catalogo, { texto: clave, tamano });
    const buscada = clave.trim().toLowerCase();
    return pagina.filas.find((fila) => String(fila['clave']).toLowerCase() === buscada) ?? null;
  }
}

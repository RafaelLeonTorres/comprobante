import { HttpResourceRef, httpResource } from '@angular/common/http';
import { Injectable } from '@angular/core';

/** GET /api/health */
export interface Salud {
  estado: string;
}

/** La versión de los catálogos del SAT que hay en la base de datos. */
export interface VersionCatalogos {
  archivo: string;
  /** Fecha de publicación del Excel del SAT, aaaa-mm-dd. */
  publicacion: string;
  cargadoEn: string;
}

export interface ResumenCatalogo {
  nombre: string;
  titulo: string;
  filas: number;
}

/** GET /api/catalogos */
export interface Catalogos {
  version: VersionCatalogos | null;
  catalogos: ResumenCatalogo[];
}

/** Lo que la pantalla de inicio pregunta a la API para saber que todo está en pie. */
@Injectable({ providedIn: 'root' })
export class EstadoApi {
  /** Se llama desde un campo del componente: httpResource necesita el contexto de inyección. */
  salud(): HttpResourceRef<Salud | undefined> {
    return httpResource<Salud>(() => '/api/health');
  }

  catalogos(): HttpResourceRef<Catalogos | undefined> {
    return httpResource<Catalogos>(() => '/api/catalogos');
  }
}

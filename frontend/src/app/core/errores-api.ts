import { HttpErrorResponse } from '@angular/common/http';

/**
 * Un fallo de la API ya explicado en español. <c>campos</c> trae los errores por campo (<c>conceptos[0].cantidad</c>) cuando
 * la API respondió 400 con los datos que hay que corregir.
 */
export class ErrorApi extends Error {
  constructor(
    readonly estado: number,
    readonly titulo: string,
    readonly detalle?: string,
    readonly campos: Readonly<Record<string, readonly string[]>> = {},
  ) {
    super(detalle ? `${titulo}: ${detalle}` : titulo);
    this.name = 'ErrorApi';
  }

  /** Cuántos mensajes por campo trae. */
  get totalDeCampos(): number {
    return Object.keys(this.campos).length;
  }
}

interface ProblemaApi {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

function esProblema(cuerpo: unknown): cuerpo is ProblemaApi {
  return typeof cuerpo === 'object' && cuerpo !== null && ('title' in cuerpo || 'detail' in cuerpo || 'errors' in cuerpo);
}

/** Convierte lo que lance <c>HttpClient</c> (o cualquier otra cosa) en un <c>ErrorApi</c> que se pueda mostrar tal cual. */
export function aErrorApi(error: unknown): ErrorApi {
  if (error instanceof ErrorApi) {
    return error;
  }

  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return new ErrorApi(
        0,
        'No se pudo conectar con la API',
        'Revisa que los contenedores estén corriendo (docker compose ps) y vuelve a intentarlo.',
      );
    }

    const problema = esProblema(error.error) ? error.error : undefined;
    if (error.status === 429) {
      return new ErrorApi(429, 'Demasiadas solicitudes', problema?.detail ?? 'Espera unos segundos y vuelve a intentarlo.');
    }

    if (error.status >= 500) {
      return new ErrorApi(
        error.status,
        problema?.title ?? 'La API tuvo un problema',
        problema?.detail ?? 'No es un error de tus datos: vuelve a intentarlo en un momento.',
      );
    }

    return new ErrorApi(
      error.status,
      problema?.title ?? 'La API rechazó la solicitud',
      problema?.detail,
      problema?.errors ?? {},
    );
  }

  return new ErrorApi(-1, 'Algo salió mal', error instanceof Error ? error.message : undefined);
}

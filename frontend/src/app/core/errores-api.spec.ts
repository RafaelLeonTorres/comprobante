import { HttpErrorResponse } from '@angular/common/http';

import { ErrorApi, aErrorApi } from './errores-api';

function respuesta(estado: number, cuerpo: unknown): HttpErrorResponse {
  return new HttpErrorResponse({ status: estado, statusText: 'x', error: cuerpo });
}

describe('aErrorApi', () => {
  it('un 400 conserva el título, el detalle y los errores por campo', () => {
    const error = aErrorApi(
      respuesta(400, {
        title: 'Hay datos que corregir',
        detail: 'Revisa los campos marcados y vuelve a intentarlo.',
        errors: { 'conceptos[0].cantidad': ['La cantidad debe ser mayor que cero.'], moneda: ['La moneda ZZZ no está en el catálogo.'] },
      }),
    );

    expect(error.estado).toBe(400);
    expect(error.titulo).toBe('Hay datos que corregir');
    expect(error.detalle).toContain('Revisa los campos');
    expect(error.campos['conceptos[0].cantidad']).toEqual(['La cantidad debe ser mayor que cero.']);
    expect(error.totalDeCampos).toBe(2);
    expect(error.message).toBe('Hay datos que corregir: Revisa los campos marcados y vuelve a intentarlo.');
  });

  it('sin conexión (estado 0) explica cómo revisar los contenedores', () => {
    const error = aErrorApi(respuesta(0, null));

    expect(error.estado).toBe(0);
    expect(error.titulo).toBe('No se pudo conectar con la API');
    expect(error.detalle).toContain('docker compose ps');
  });

  it('un 413 trae el motivo de la API', () => {
    const error = aErrorApi(respuesta(413, { title: 'XML demasiado grande', detail: 'El XML pesa más de 2 MB.' }));

    expect(error.estado).toBe(413);
    expect(error.titulo).toBe('XML demasiado grande');
    expect(error.detalle).toBe('El XML pesa más de 2 MB.');
    expect(error.totalDeCampos).toBe(0);
  });

  it('un 429 pide esperar', () => {
    const error = aErrorApi(respuesta(429, null));

    expect(error.estado).toBe(429);
    expect(error.titulo).toBe('Demasiadas solicitudes');
    expect(error.detalle).toContain('Espera');
  });

  it('un 500 aclara que no es culpa de los datos, aunque no traiga cuerpo', () => {
    const error = aErrorApi(respuesta(502, '<html>Bad Gateway</html>'));

    expect(error.estado).toBe(502);
    expect(error.titulo).toBe('La API tuvo un problema');
    expect(error.detalle).toContain('No es un error de tus datos');
  });

  it('un ErrorApi pasa tal cual', () => {
    const original = new ErrorApi(400, 'Algo');

    expect(aErrorApi(original)).toBe(original);
  });

  it('cualquier otra cosa se vuelve un error genérico con su mensaje', () => {
    expect(aErrorApi(new Error('se rompió')).detalle).toBe('se rompió');
    expect(aErrorApi('texto').titulo).toBe('Algo salió mal');
  });
});

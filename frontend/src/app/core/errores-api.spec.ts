import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';

import { ErrorApi, aErrorApi, conCuerpoLegible } from './errores-api';

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

  it('un 429 con el detalle de la API lo muestra tal cual, con cuánto esperar', () => {
    const detalle = 'Hiciste demasiadas solicitudes en poco tiempo. Espera 17 segundos e inténtalo de nuevo.';
    const error = aErrorApi(respuesta(429, { title: 'Demasiadas solicitudes', detail: detalle }));

    expect(error.estado).toBe(429);
    expect(error.titulo).toBe('Demasiadas solicitudes');
    expect(error.detalle).toBe(detalle);
    expect(error.totalDeCampos).toBe(0);
  });

  it('un 429 de nginx, con su problem+json, también se explica', () => {
    const error = aErrorApi(
      respuesta(429, {
        title: 'Demasiadas solicitudes',
        status: 429,
        detail: 'Hiciste demasiadas solicitudes en poco tiempo. Espera un segundo e inténtalo de nuevo.',
      }),
    );

    expect(error.detalle).toContain('Espera un segundo');
  });

  it('un 413 de nginx, con su problem+json, conserva el motivo', () => {
    const error = aErrorApi(
      respuesta(413, { title: 'El contenido es demasiado grande', status: 413, detail: 'Lo que enviaste pesa más de lo que acepta la API.' }),
    );

    expect(error.estado).toBe(413);
    expect(error.titulo).toBe('El contenido es demasiado grande');
    expect(error.detalle).toContain('pesa más');
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

describe('conCuerpoLegible', () => {
  function respuestaEnBlob(estado: number, texto: string): HttpErrorResponse {
    return new HttpErrorResponse({
      status: estado,
      statusText: 'x',
      url: '/api/representaciones/pdf',
      error: new Blob([texto], { type: 'application/problem+json' }),
    });
  }

  it('un error con el cuerpo en Blob se vuelve a leer como JSON y se explica igual que los demás', async () => {
    const problema = { title: 'Hay datos que corregir', errors: { logo: ['El logo debe ser una imagen PNG o JPEG.'] } };

    const error = aErrorApi(await conCuerpoLegible(respuestaEnBlob(400, JSON.stringify(problema))));

    expect(error.estado).toBe(400);
    expect(error.titulo).toBe('Hay datos que corregir');
    expect(error.campos['logo']).toEqual(['El logo debe ser una imagen PNG o JPEG.']);
  });

  it('conserva el estado, las cabeceras y la dirección del error original', async () => {
    const original = new HttpErrorResponse({
      status: 429,
      statusText: 'Too Many Requests',
      headers: new HttpHeaders({ 'Retry-After': '3' }),
      url: '/api/representaciones/pdf',
      error: new Blob(['{"detail":"Espera unos segundos."}']),
    });

    const legible = (await conCuerpoLegible(original)) as HttpErrorResponse;

    expect(legible.status).toBe(429);
    expect(legible.statusText).toBe('Too Many Requests');
    expect(legible.headers.get('Retry-After')).toBe('3');
    expect(legible.url).toBe('/api/representaciones/pdf');
    expect(aErrorApi(legible).detalle).toBe('Espera unos segundos.');
  });

  it('un cuerpo que no es JSON (la página de error de un intermediario) se explica solo con el código', async () => {
    const error = aErrorApi(await conCuerpoLegible(respuestaEnBlob(502, '<html>Bad Gateway</html>')));

    expect(error.estado).toBe(502);
    expect(error.titulo).toBe('La API tuvo un problema');
  });

  it('lo que no es un error con Blob se devuelve tal cual', async () => {
    const sinBlob = respuesta(400, { title: 'x' });
    const otro = new Error('no es de HTTP');

    expect(await conCuerpoLegible(sinBlob)).toBe(sinBlob);
    expect(await conCuerpoLegible(otro)).toBe(otro);
  });
});

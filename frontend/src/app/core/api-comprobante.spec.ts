import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ApiComprobante } from './api-comprobante';
import { ErrorApi } from './errores-api';
import { SolicitudCfdi } from './modelos';

describe('ApiComprobante', () => {
  function preparar() {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    return { api: TestBed.inject(ApiComprobante), http: TestBed.inject(HttpTestingController) };
  }

  const solicitud: SolicitudCfdi = { emisor: { rfc: 'EKU9003173C9' }, conceptos: [] };

  it('generar manda la solicitud como JSON y devuelve el resultado', async () => {
    const { api, http } = preparar();

    const promesa = api.generar(solicitud);
    const peticion = http.expectOne('/api/comprobantes');
    expect(peticion.request.method).toBe('POST');
    expect(peticion.request.body).toEqual(solicitud);
    peticion.flush({ xml: '<cfdi:Comprobante/>', nombreArchivo: 'EKU9003173C9-A-1-prueba.xml' });

    const resultado = await promesa;
    expect(resultado.nombreArchivo).toBe('EKU9003173C9-A-1-prueba.xml');
  });

  it('generar explica los datos que hay que corregir', async () => {
    const { api, http } = preparar();

    const promesa = api.generar(solicitud);
    http.expectOne('/api/comprobantes').flush(
      { title: 'Hay datos que corregir', errors: { 'emisor.rfc': ['Falta el RFC del emisor.'] } },
      { status: 400, statusText: 'Bad Request' },
    );

    const error = await promesa.catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ErrorApi);
    expect((error as ErrorApi).campos['emisor.rfc']).toEqual(['Falta el RFC del emisor.']);
  });

  it('validar manda el XML tal cual, con su tipo de contenido y la codificación', async () => {
    const { api, http } = preparar();
    const xml = '<?xml version="1.0"?><cfdi:Comprobante Nombre="Ñandú"/>';

    const promesa = api.validar(xml);
    const peticion = http.expectOne('/api/validaciones');
    expect(peticion.request.method).toBe('POST');
    expect(peticion.request.body).toBe(xml);
    expect(peticion.request.headers.get('Content-Type')).toBe('application/xml; charset=utf-8');
    peticion.flush({ esValido: true, hallazgos: [], cadenaOriginal: '||4.0||', selloValido: true, certificado: null });

    expect((await promesa).esValido).toBe(true);
  });

  it('validar con un XML de más de 2 MB da el error 413 de la API', async () => {
    const { api, http } = preparar();

    const promesa = api.validar('<a/>');
    http.expectOne('/api/validaciones').flush(
      { title: 'El XML es demasiado grande', detail: 'El XML pesa más de 2 MB.' },
      { status: 413, statusText: 'Payload Too Large' },
    );

    const error = (await promesa.catch((e: unknown) => e)) as ErrorApi;
    expect(error.estado).toBe(413);
    expect(error.detalle).toBe('El XML pesa más de 2 MB.');
  });

  it('el ejemplo se pide como texto a /ejemplos', async () => {
    const { api, http } = preparar();

    const promesa = api.ejemplo('ingreso.xml');
    const peticion = http.expectOne('/ejemplos/ingreso.xml');
    expect(peticion.request.responseType).toBe('text');
    peticion.flush('<cfdi:Comprobante/>');

    expect(await promesa).toBe('<cfdi:Comprobante/>');
  });
});

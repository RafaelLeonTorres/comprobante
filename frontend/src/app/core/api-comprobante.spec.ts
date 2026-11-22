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

describe('ApiComprobante.pdf', () => {
  function preparar() {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    return { api: TestBed.inject(ApiComprobante), http: TestBed.inject(HttpTestingController) };
  }

  const XML = '<?xml version="1.0"?><cfdi:Comprobante Nombre="Ñandú"/>';
  const CONTENIDO_DEL_PDF = '%PDF-1.7 prueba';

  function pdf(): Blob {
    return new Blob([CONTENIDO_DEL_PDF], { type: 'application/pdf' });
  }

  it('manda el XML, el logo y la opción en un formulario y pide la respuesta como Blob', async () => {
    const { api, http } = preparar();
    const logo = new File([new Uint8Array([137, 80, 78, 71])], 'mi-logo.png', { type: 'image/png' });

    const promesa = api.pdf(XML, { logo, incluirCadenaYSellos: false });
    const peticion = http.expectOne('/api/representaciones/pdf');
    expect(peticion.request.method).toBe('POST');
    expect(peticion.request.responseType).toBe('blob');
    const formulario = peticion.request.body as FormData;
    expect(formulario).toBeInstanceOf(FormData);
    const xml = formulario.get('xml') as File;
    expect(xml.name).toBe('comprobante.xml');
    expect(xml.type).toBe('application/xml');
    expect(await xml.text()).toBe(XML);
    expect((formulario.get('logo') as File).name).toBe('mi-logo.png');
    expect(formulario.get('incluirCadenaYSellos')).toBe('false');
    peticion.flush(pdf());

    await promesa;
  });

  it('sin logo, o con un logo nulo, no manda el campo del logo', async () => {
    const { api, http } = preparar();

    const promesa = api.pdf(XML, { incluirCadenaYSellos: true });
    const peticion = http.expectOne('/api/representaciones/pdf');
    const formulario = peticion.request.body as FormData;
    expect(formulario.has('logo')).toBe(false);
    expect(formulario.get('incluirCadenaYSellos')).toBe('true');
    peticion.flush(pdf());
    await promesa;

    const conNulo = api.pdf(XML, { logo: null, incluirCadenaYSellos: true });
    const otra = http.expectOne('/api/representaciones/pdf');
    expect((otra.request.body as FormData).has('logo')).toBe(false);
    otra.flush(pdf());
    await conNulo;
  });

  it('devuelve el PDF con el nombre que propone la API', async () => {
    const { api, http } = preparar();

    const promesa = api.pdf(XML, { incluirCadenaYSellos: true });
    http
      .expectOne('/api/representaciones/pdf')
      .flush(pdf(), { headers: { 'Content-Disposition': 'attachment; filename="EKU9003173C9-A-123-prueba.pdf"' } });

    const resultado = await promesa;
    expect(resultado.nombre).toBe('EKU9003173C9-A-123-prueba.pdf');
    expect(resultado.archivo.type).toBe('application/pdf');
    expect(await resultado.archivo.text()).toBe(CONTENIDO_DEL_PDF);
  });

  it('si la API no propone nombre, usa uno por omisión', async () => {
    const { api, http } = preparar();

    const promesa = api.pdf(XML, { incluirCadenaYSellos: true });
    http.expectOne('/api/representaciones/pdf').flush(pdf());

    expect((await promesa).nombre).toBe('comprobante.pdf');
  });

  it('un 400 llega con el cuerpo leído: cada error en su campo', async () => {
    const { api, http } = preparar();

    const promesa = api.pdf(XML, { incluirCadenaYSellos: true });
    http.expectOne('/api/representaciones/pdf').flush(
      new Blob([JSON.stringify({ title: 'Hay datos que corregir', errors: { logo: ['El logo pesa más de 200 KB; sube uno más ligero.'] } })], {
        type: 'application/problem+json',
      }),
      { status: 400, statusText: 'Bad Request' },
    );

    const error = (await promesa.catch((e: unknown) => e)) as ErrorApi;
    expect(error).toBeInstanceOf(ErrorApi);
    expect(error.estado).toBe(400);
    expect(error.campos['logo']).toEqual(['El logo pesa más de 200 KB; sube uno más ligero.']);
  });

  it('un 429 trae el motivo de la API', async () => {
    const { api, http } = preparar();

    const promesa = api.pdf(XML, { incluirCadenaYSellos: true });
    http.expectOne('/api/representaciones/pdf').flush(
      new Blob([JSON.stringify({ title: 'Demasiadas solicitudes', detail: 'Se están dibujando otros PDF; espera unos segundos.' })]),
      { status: 429, statusText: 'Too Many Requests' },
    );

    const error = (await promesa.catch((e: unknown) => e)) as ErrorApi;
    expect(error.estado).toBe(429);
    expect(error.detalle).toBe('Se están dibujando otros PDF; espera unos segundos.');
  });

  it('sin conexión explica cómo revisar los contenedores', async () => {
    const { api, http } = preparar();

    const promesa = api.pdf(XML, { incluirCadenaYSellos: true });
    http.expectOne('/api/representaciones/pdf').error(new ProgressEvent('error'), { status: 0, statusText: '' });

    const error = (await promesa.catch((e: unknown) => e)) as ErrorApi;
    expect(error.estado).toBe(0);
    expect(error.titulo).toBe('No se pudo conectar con la API');
  });
});

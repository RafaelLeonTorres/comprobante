import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ServicioCatalogos, descripcionDe } from './catalogos';
import { ErrorApi } from './errores-api';
import { PaginaCatalogo } from './modelos';

const PAGINA: PaginaCatalogo = {
  total: 2,
  pagina: 1,
  tamano: 10,
  columnas: [
    { clave: 'clave', titulo: 'Clave' },
    { clave: 'descripcion', titulo: 'Descripción' },
  ],
  filas: [
    { clave: 'G03', descripcion: 'Gastos en general.' },
    { clave: 'G030', descripcion: 'Otra' },
  ],
};

describe('ServicioCatalogos', () => {
  function crear() {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    return { servicio: TestBed.inject(ServicioCatalogos), http: TestBed.inject(HttpTestingController) };
  }

  it('pide el catálogo con el texto, la página y el tamaño', async () => {
    const { servicio, http } = crear();

    const resultado = servicio.buscar('uso-cfdi', { texto: ' g03 ', pagina: 2, tamano: 5 });
    const llamada = http.expectOne((r) => r.url === '/api/catalogos/uso-cfdi');
    expect(llamada.request.params.get('q')).toBe('g03');
    expect(llamada.request.params.get('pagina')).toBe('2');
    expect(llamada.request.params.get('tamano')).toBe('5');
    llamada.flush(PAGINA);

    expect(await resultado).toEqual(PAGINA);
  });

  it('sin texto no manda q', async () => {
    const { servicio, http } = crear();

    const resultado = servicio.buscar('moneda');
    const llamada = http.expectOne((r) => r.url === '/api/catalogos/moneda');
    expect(llamada.request.params.has('q')).toBe(false);
    expect(llamada.request.params.get('tamano')).toBe('10');
    llamada.flush(PAGINA);
    await resultado;
  });

  it('la misma consulta se responde de memoria, aunque cambien mayúsculas o espacios', async () => {
    const { servicio, http } = crear();

    const primera = servicio.buscar('uso-cfdi', { texto: 'G03' });
    const segunda = servicio.buscar('uso-cfdi', { texto: ' g03 ' });
    http.expectOne((r) => r.url === '/api/catalogos/uso-cfdi').flush(PAGINA);

    expect(await primera).toBe(await segunda);
    http.verify();
  });

  it('consultas distintas son peticiones distintas', async () => {
    const { servicio, http } = crear();

    void servicio.buscar('uso-cfdi', { texto: 'G03' });
    void servicio.buscar('uso-cfdi', { texto: 'G01' });
    void servicio.buscar('forma-pago', { texto: 'G03' });

    expect(http.match(() => true)).toHaveLength(3);
  });

  it('un fallo sale explicado y no se recuerda: la siguiente vez se vuelve a pedir', async () => {
    const { servicio, http } = crear();

    const fallida = servicio.buscar('uso-cfdi', { texto: 'G03' });
    http.expectOne((r) => r.url === '/api/catalogos/uso-cfdi').flush(null, { status: 500, statusText: 'Error' });
    await expect(fallida).rejects.toBeInstanceOf(ErrorApi);

    const otra = servicio.buscar('uso-cfdi', { texto: 'G03' });
    http.expectOne((r) => r.url === '/api/catalogos/uso-cfdi').flush(PAGINA);
    expect(await otra).toEqual(PAGINA);
  });

  it('describir devuelve la fila de la clave exacta y no la de otra que la contiene', async () => {
    const { servicio, http } = crear();

    const fila = servicio.describir('uso-cfdi', 'g03');
    http.expectOne((r) => r.url === '/api/catalogos/uso-cfdi').flush(PAGINA);

    expect(await fila).toEqual({ clave: 'G03', descripcion: 'Gastos en general.' });
  });

  it('describir devuelve null si la clave no está', async () => {
    const { servicio, http } = crear();

    const fila = servicio.describir('uso-cfdi', 'Z99');
    http.expectOne((r) => r.url === '/api/catalogos/uso-cfdi').flush({ ...PAGINA, total: 0, filas: [] });

    expect(await fila).toBeNull();
  });
});

describe('descripcionDe', () => {
  it('toma descripcion o, si no hay, nombre', () => {
    expect(descripcionDe({ clave: 'G03', descripcion: 'Gastos en general.' })).toBe('Gastos en general.');
    expect(descripcionDe({ clave: 'E48', nombre: 'Unidad de servicio' })).toBe('Unidad de servicio');
    expect(descripcionDe({ clave: 'x' })).toBe('');
  });
});

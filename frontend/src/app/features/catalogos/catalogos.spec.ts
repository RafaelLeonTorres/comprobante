import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { Catalogos as CatalogosDeLaApi } from '../../core/estado-api';
import { registrarIconos } from '../../core/iconos';
import { provideIdioma } from '../../core/idioma';
import { FilaCatalogo, PaginaCatalogo } from '../../core/modelos';
import { Catalogos } from './catalogos';

const RESUMEN: CatalogosDeLaApi = {
  version: { archivo: 'catCFDI_V_4_20261120.xls', publicacion: '2026-11-20', cargadoEn: '2026-11-21T20:00:00Z' },
  catalogos: [
    { nombre: 'uso-cfdi', titulo: 'Uso del CFDI (c_UsoCFDI)', filas: 24 },
    { nombre: 'moneda', titulo: 'Moneda (c_Moneda)', filas: 183 },
    { nombre: 'clave-prod-serv', titulo: 'Clave de producto o servicio (c_ClaveProdServ)', filas: 52513 },
  ],
};

function pagina(total: number, filas: FilaCatalogo[], numero = 1): PaginaCatalogo {
  return {
    total,
    pagina: numero,
    tamano: 25,
    columnas: [
      { clave: 'clave', titulo: 'Clave' },
      { clave: 'descripcion', titulo: 'Descripción' },
      { clave: 'fisica', titulo: 'Persona física' },
    ],
    filas,
  };
}

const G01: FilaCatalogo = { clave: 'G01', descripcion: 'Adquisición de mercancías.', fisica: true };
const D01: FilaCatalogo = { clave: 'D01', descripcion: 'Honorarios médicos.', fisica: false };
const MXN: FilaCatalogo = { clave: 'MXN', descripcion: 'Peso Mexicano', fisica: null };

describe('Catalogos', () => {
  async function abrir(url: string) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'catalogos', component: Catalogos }], withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideIdioma(),
      ],
    });
    registrarIconos(TestBed.inject(MatIconRegistry), TestBed.inject(DomSanitizer));
    const http = TestBed.inject(HttpTestingController);
    const router = TestBed.inject(Router);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, Catalogos);
    harness.detectChanges();
    const elemento = () => harness.routeNativeElement as HTMLElement;

    /** Deja correr las promesas y pinta. No usa `whenStable()`: las peticiones HTTP de la prueba se mantienen abiertas. */
    const refrescar = async (espera = 0) => {
      await new Promise((resolve) => setTimeout(resolve, espera));
      TestBed.tick();
      harness.detectChanges();
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.tick();
      harness.detectChanges();
    };
    const lista = async () => {
      const peticion = await vi.waitFor(() => {
        const encontradas = http.match('/api/catalogos');
        if (encontradas.length === 0) {
          throw new Error('todavía no se pide la lista de catálogos');
        }

        return encontradas[0]!;
      });
      peticion.flush(RESUMEN);
      await refrescar();
    };
    const consulta = (catalogo: string, esperado: { q?: string; pagina?: string } = {}): Promise<TestRequest> =>
      vi.waitFor(() => {
        const encontradas = http.match(
          (r) =>
            r.url === `/api/catalogos/${catalogo}` &&
            r.params.get('pagina') === (esperado.pagina ?? '1') &&
            (r.params.get('q') ?? undefined) === esperado.q,
        );
        if (encontradas.length === 0) {
          throw new Error(`todavía no hay consulta de ${catalogo} ${JSON.stringify(esperado)}`);
        }

        return encontradas.at(-1)!;
      });
    const caja = () => elemento().querySelector<HTMLInputElement>('input[type="search"]')!;
    const escribir = async (texto: string) => {
      caja().value = texto;
      caja().dispatchEvent(new Event('input'));
      await refrescar();
    };
    const boton = (texto: string) =>
      [...elemento().querySelectorAll<HTMLButtonElement>('button')].find((b) => (b.textContent!.trim() + (b.getAttribute('aria-label') ?? '')).includes(texto))!;
    const limpio = (nodo: Element | null | undefined) => nodo!.textContent!.replace(/\s+/g, ' ').trim();
    const filas = () => [...elemento().querySelectorAll('tbody tr')].map((fila) => [...fila.querySelectorAll('td')].map((celda) => limpio(celda)));
    return { http, router, harness, elemento, refrescar, lista, consulta, caja, escribir, boton, limpio, filas };
  }

  afterEach(() => {
    document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = ''));
  });

  describe('la lista', () => {
    it('mientras llega dice que carga', async () => {
      const { elemento } = await abrir('/catalogos');

      expect(elemento().textContent).toContain('Cargando catálogos…');
    });

    it('muestra cada catálogo con su número de filas, un enlace que lo abre y la versión publicada', async () => {
      const { elemento, lista, limpio } = await abrir('/catalogos');
      await lista();

      expect(limpio(elemento().querySelector('h2'))).toBe('3 catálogos y 52,720 filas');
      const tarjetas = [...elemento().querySelectorAll('a.tarjeta')];
      expect(tarjetas.map((t) => limpio(t))).toEqual([
        'Uso del CFDI (c_UsoCFDI) 24 filas',
        'Moneda (c_Moneda) 183 filas',
        'Clave de producto o servicio (c_ClaveProdServ) 52,513 filas',
      ]);
      expect(tarjetas.map((t) => t.getAttribute('href'))).toEqual([
        '/catalogos?catalogo=uso-cfdi',
        '/catalogos?catalogo=moneda',
        '/catalogos?catalogo=clave-prod-serv',
      ]);
      expect(limpio(elemento().querySelector('.intro'))).toContain('Versión publicada el 20 de noviembre de 2026 (catCFDI_V_4_20261120.xls).');
    });

    it('si la API no responde lo dice y explica qué revisar', async () => {
      const { elemento, http, refrescar } = await abrir('/catalogos');

      (await vi.waitFor(() => http.expectOne('/api/catalogos'))).flush(null, { status: 502, statusText: 'Bad Gateway' });
      await refrescar();

      expect(elemento().querySelector('[role="alert"]')!.textContent).toContain('docker compose ps');
    });

    it('un nombre de catálogo que no existe se dice y no se consulta', async () => {
      const { elemento, http, lista } = await abrir('/catalogos?catalogo=nada');
      await lista();

      expect(elemento().querySelector('[role="alert"]')!.textContent).toContain('No existe el catálogo «nada»');
      expect(elemento().querySelector('a[href="/catalogos"]')).not.toBeNull();
      http.expectNone((r) => r.url.startsWith('/api/catalogos/'));
    });

    it('al abrirla de frente no le quita el foco a nadie', async () => {
      const { lista } = await abrir('/catalogos');
      await lista();

      expect(document.activeElement).toBe(document.body);
    });
  });

  describe('un catálogo', () => {
    it('pide la primera página de 25 filas y la pinta con sus columnas y los sí/no en español', async () => {
      const { elemento, lista, consulta, refrescar, filas, limpio } = await abrir('/catalogos?catalogo=uso-cfdi');
      await lista();

      const peticion = await consulta('uso-cfdi');
      expect(peticion.request.params.get('tamano')).toBe('25');
      peticion.flush(pagina(24, [G01, D01, MXN]));
      await refrescar();

      expect(limpio(elemento().querySelector('h2#titulo-catalogo'))).toBe('Uso del CFDI (c_UsoCFDI)');
      expect([...elemento().querySelectorAll('th')].map((t) => limpio(t))).toEqual(['Clave', 'Descripción', 'Persona física']);
      expect(filas()).toEqual([
        ['G01', 'Adquisición de mercancías.', 'Sí'],
        ['D01', 'Honorarios médicos.', 'No'],
        ['MXN', 'Peso Mexicano', ''],
      ]);
      expect(elemento().querySelector('td.mono')!.textContent).toBe('G01');
      expect(limpio(elemento().querySelector('.estado-busqueda'))).toBe('24 resultados');
      expect(elemento().querySelector('nav.paginacion')).toBeNull();
      expect(elemento().querySelector('[role="region"]')!.getAttribute('tabindex')).toBe('0');
      expect(elemento().querySelector('table')!.getAttribute('aria-labelledby')).toBe('titulo-catalogo');
    });

    it('la búsqueda y la página de la URL llegan a la API y a la caja', async () => {
      const { elemento, lista, consulta, refrescar, caja, limpio } = await abrir('/catalogos?catalogo=clave-prod-serv&q=servicio&pagina=2');
      await lista();

      (await consulta('clave-prod-serv', { q: 'servicio', pagina: '2' })).flush(pagina(2454, [G01], 2));
      await refrescar();

      expect(caja().value).toBe('servicio');
      expect(limpio(elemento().querySelector('.estado-busqueda'))).toBe('2,454 resultados para «servicio» · página 2 de 99');
    });

    it('teclear busca tras una pausa, vuelve a la primera página y deja el texto en la URL', async () => {
      const { elemento, http, router, lista, consulta, refrescar, escribir, limpio } = await abrir('/catalogos?catalogo=moneda&pagina=2');
      await lista();
      (await consulta('moneda', { pagina: '2' })).flush(pagina(183, [MXN], 2));
      await refrescar();

      await escribir('peso');
      expect(router.url).toBe('/catalogos?catalogo=moneda&pagina=2');
      http.expectNone((r) => r.url === '/api/catalogos/moneda' && r.params.has('q'));

      await vi.waitFor(() => expect(router.url).toBe('/catalogos?catalogo=moneda&q=peso'));
      (await consulta('moneda', { q: 'peso' })).flush(pagina(3, [MXN]));
      await refrescar();

      expect(limpio(elemento().querySelector('.estado-busqueda'))).toBe('3 resultados para «peso»');
    });

    it('Enter busca de inmediato, sin esperar la pausa', async () => {
      const { elemento, router, lista, consulta, refrescar, escribir } = await abrir('/catalogos?catalogo=moneda');
      await lista();
      (await consulta('moneda')).flush(pagina(183, [MXN]));
      await refrescar();

      await escribir('mxn');
      const envio = new Event('submit', { cancelable: true });
      elemento().querySelector('form')!.dispatchEvent(envio);
      await vi.waitFor(() => expect(router.url).toBe('/catalogos?catalogo=moneda&q=mxn'));

      expect(envio.defaultPrevented).toBe(true);
    });

    it('borrar la búsqueda la quita de la URL, vacía la caja y le devuelve el foco', async () => {
      const { router, lista, consulta, refrescar, caja, boton } = await abrir('/catalogos?catalogo=moneda&q=peso');
      await lista();
      (await consulta('moneda', { q: 'peso' })).flush(pagina(3, [MXN]));
      await refrescar();
      expect(caja().value).toBe('peso');

      boton('Borrar la búsqueda').click();
      await vi.waitFor(() => expect(router.url).toBe('/catalogos?catalogo=moneda'));
      await refrescar();

      expect(caja().value).toBe('');
      expect(document.activeElement).toBe(caja());
    });

    it('si cambia la URL por fuera (el botón de atrás), la caja la sigue', async () => {
      const { router, lista, consulta, refrescar, caja } = await abrir('/catalogos?catalogo=moneda&q=peso');
      await lista();
      (await consulta('moneda', { q: 'peso' })).flush(pagina(3, [MXN]));
      await refrescar();

      await router.navigateByUrl('/catalogos?catalogo=moneda&q=dolar');
      await refrescar();

      expect(caja().value).toBe('dolar');
      (await consulta('moneda', { q: 'dolar' })).flush(pagina(2, [MXN]));
    });

    it('con varias páginas navega con Anterior y Siguiente, y mientras llega la página nueva deja la anterior atenuada', async () => {
      const { elemento, router, lista, consulta, refrescar, boton, limpio } = await abrir('/catalogos?catalogo=moneda');
      await lista();
      (await consulta('moneda')).flush(pagina(60, [MXN]));
      await refrescar();

      expect(limpio(elemento().querySelector('nav.paginacion'))).toBe('Anterior Página 1 de 3 Siguiente');
      expect(boton('Anterior').getAttribute('aria-disabled')).toBe('true');
      expect(boton('Siguiente').getAttribute('aria-disabled')).not.toBe('true');

      boton('Siguiente').click();
      await vi.waitFor(() => expect(router.url).toBe('/catalogos?catalogo=moneda&pagina=2'));
      await refrescar();
      expect(elemento().querySelector('.tabla.atenuada')).not.toBeNull();
      expect(elemento().querySelectorAll('tbody tr')).toHaveLength(1);
      expect(elemento().querySelector('mat-progress-bar')).not.toBeNull();

      (await consulta('moneda', { pagina: '2' })).flush(pagina(60, [D01], 2));
      await refrescar();
      expect(elemento().querySelector('.tabla.atenuada')).toBeNull();
      expect(elemento().querySelector('mat-progress-bar')).toBeNull();
      expect(limpio(elemento().querySelector('nav.paginacion'))).toBe('Anterior Página 2 de 3 Siguiente');
      expect(limpio(elemento().querySelector('.estado-busqueda'))).toBe('60 resultados · página 2 de 3');

      boton('Anterior').click();
      await vi.waitFor(() => expect(router.url).toBe('/catalogos?catalogo=moneda'));
    });

    it('en la última página no hay a dónde avanzar', async () => {
      const { lista, consulta, refrescar, boton } = await abrir('/catalogos?catalogo=moneda&pagina=3');
      await lista();
      (await consulta('moneda', { pagina: '3' })).flush(pagina(60, [MXN], 3));
      await refrescar();

      expect(boton('Siguiente').getAttribute('aria-disabled')).toBe('true');
      expect(boton('Anterior').getAttribute('aria-disabled')).not.toBe('true');
    });

    it('una página que no existe lo dice y ofrece volver a la primera', async () => {
      const { elemento, router, lista, consulta, refrescar, boton, limpio } = await abrir('/catalogos?catalogo=moneda&pagina=9');
      await lista();
      (await consulta('moneda', { pagina: '9' })).flush(pagina(60, [], 9));
      await refrescar();

      expect(limpio(elemento().querySelector('.nota:not(:first-of-type)') ?? elemento().querySelector('.detalle'))).toContain('Esta página no existe: el catálogo tiene 3 páginas con esta búsqueda.');
      boton('Ir a la primera').click();
      await vi.waitFor(() => expect(router.url).toBe('/catalogos?catalogo=moneda'));
    });

    it('un número de página que no es número se toma como la primera', async () => {
      const { lista, consulta } = await abrir('/catalogos?catalogo=moneda&pagina=abc');
      await lista();

      expect((await consulta('moneda')).request.params.get('pagina')).toBe('1');
    });

    it('si nada coincide lo dice con el texto buscado y ofrece borrar la búsqueda', async () => {
      const { elemento, lista, consulta, refrescar, boton, limpio } = await abrir('/catalogos?catalogo=moneda&q=xyz');
      await lista();
      (await consulta('moneda', { q: 'xyz' })).flush(pagina(0, []));
      await refrescar();

      expect(limpio(elemento().querySelector('.detalle'))).toContain('Ninguna fila coincide con «xyz».');
      expect(limpio(elemento().querySelector('.estado-busqueda'))).toBe('0 resultados para «xyz»');
      expect(boton('Borrar la búsqueda')).toBeDefined();
      expect(elemento().querySelector('table')).toBeNull();
    });

    it('un fallo de la API en la consulta se avisa y no deja una tabla a medias', async () => {
      const { elemento, lista, consulta, refrescar } = await abrir('/catalogos?catalogo=moneda');
      await lista();

      (await consulta('moneda')).flush({ title: 'La consulta falló' }, { status: 500, statusText: 'Error' });
      await refrescar();

      expect(elemento().querySelector('[role="alert"]')!.textContent).toContain('La consulta falló');
      expect(elemento().querySelector('table')).toBeNull();
    });
  });

  describe('el foco al cambiar de vista', () => {
    it('elegir un catálogo lleva el foco a su título y volver a la lista, al título de la pantalla', async () => {
      const { elemento, router, lista, consulta, refrescar } = await abrir('/catalogos');
      await lista();

      elemento().querySelector<HTMLAnchorElement>('a.tarjeta[href="/catalogos?catalogo=moneda"]')!.click();
      await vi.waitFor(() => expect(router.url).toBe('/catalogos?catalogo=moneda'));
      (await consulta('moneda')).flush(pagina(183, [MXN]));
      await refrescar();
      await vi.waitFor(() => expect(document.activeElement).toBe(elemento().querySelector('#titulo-catalogo')));

      elemento().querySelector<HTMLAnchorElement>('a.volver')!.click();
      await vi.waitFor(() => expect(router.url).toBe('/catalogos'));
      await refrescar();
      await vi.waitFor(() => expect(document.activeElement).toBe(elemento().querySelector('h1')));
    });
  });
});

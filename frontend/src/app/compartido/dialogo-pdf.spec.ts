import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

import { registrarIconos } from '../core/iconos';
import { PreferenciasDelPdf } from '../core/preferencias-del-pdf';
import { DialogoPdf, errorDelLogo } from './dialogo-pdf';

const XML = '<?xml version="1.0" encoding="utf-8"?>\n<cfdi:Comprobante Version="4.0"/>';
const URL_DEL_PDF = '/api/representaciones/pdf';

function png(nombre = 'logo.png', bytes = 64): File {
  return new File([new Uint8Array(bytes)], nombre, { type: 'image/png' });
}

describe('errorDelLogo', () => {
  it('acepta un PNG y un JPEG ligeros', () => {
    expect(errorDelLogo(png())).toBeNull();
    expect(errorDelLogo(new File([new Uint8Array(10)], 'logo.jpg', { type: 'image/jpeg' }))).toBeNull();
  });

  it('sin tipo, lo decide la extensión', () => {
    expect(errorDelLogo(new File([new Uint8Array(10)], 'LOGO.PNG'))).toBeNull();
    expect(errorDelLogo(new File([new Uint8Array(10)], 'logo.jpeg'))).toBeNull();
    expect(errorDelLogo(new File([new Uint8Array(10)], 'logo.gif'))).toBe('El logo debe ser una imagen PNG o JPEG.');
  });

  it('rechaza lo que no es PNG ni JPEG', () => {
    expect(errorDelLogo(new File([new Uint8Array(10)], 'logo.gif', { type: 'image/gif' }))).toBe('El logo debe ser una imagen PNG o JPEG.');
    expect(errorDelLogo(new File([new Uint8Array(10)], 'logo.svg', { type: 'image/svg+xml' }))).toBe('El logo debe ser una imagen PNG o JPEG.');
    expect(errorDelLogo(new File([new Uint8Array(10)], 'factura.pdf', { type: 'application/pdf' }))).toBe('El logo debe ser una imagen PNG o JPEG.');
  });

  it('rechaza un archivo vacío', () => {
    expect(errorDelLogo(png('vacio.png', 0))).toBe('El archivo del logo está vacío.');
  });

  it('acepta justo 200 KB y rechaza un poco más, diciendo cuánto pesa', () => {
    expect(errorDelLogo(png('justo.png', 200 * 1024))).toBeNull();
    expect(errorDelLogo(png('grande.png', 205 * 1024))).toBe('El logo pesa 205 KB; el máximo es 200 KB.');
    expect(errorDelLogo(png('grande.png', 200 * 1024 + 1))).toBe('El logo pesa 201 KB; el máximo es 200 KB.');
  });
});

describe('DialogoPdf', () => {
  const original = { crear: URL.createObjectURL, soltar: URL.revokeObjectURL };
  let descargas: string[];

  beforeEach(() => {
    descargas = [];
    URL.createObjectURL = vi.fn(() => 'blob:prueba');
    URL.revokeObjectURL = vi.fn();
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      descargas.push(this.download);
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    URL.createObjectURL = original.crear;
    URL.revokeObjectURL = original.soltar;
  });

  function abrir() {
    const ref = { close: vi.fn(), disableClose: false };
    TestBed.configureTestingModule({
      imports: [DialogoPdf],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: { xml: XML } },
        { provide: MatDialogRef, useValue: ref },
      ],
    });
    registrarIconos(TestBed.inject(MatIconRegistry), TestBed.inject(DomSanitizer));
    const http = TestBed.inject(HttpTestingController);
    return { ref, http, ...pintar() };
  }

  function pintar() {
    const fixture = TestBed.createComponent(DialogoPdf);
    fixture.detectChanges();
    const elemento = fixture.nativeElement as HTMLElement;

    /** Deja correr las promesas y pinta; no espera a `whenStable()`, que no llega mientras la prueba mantiene una petición abierta. */
    const refrescar = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.tick();
      fixture.detectChanges();
    };
    const hasta = (condicion: () => unknown) =>
      vi.waitFor(async () => {
        await refrescar();
        if (!condicion()) {
          throw new Error('todavía no');
        }
      });
    const boton = (texto: string) =>
      [...elemento.querySelectorAll<HTMLButtonElement>('button')].find((b) => b.textContent!.replace(/\s+/g, ' ').trim() === texto);
    const elegir = async (archivo: File) => {
      const entrada = elemento.querySelector<HTMLInputElement>('input[type="file"]')!;
      Object.defineProperty(entrada, 'files', { configurable: true, value: [archivo] });
      entrada.dispatchEvent(new Event('change'));
      await refrescar();
    };
    const casilla = () => elemento.querySelector<HTMLInputElement>('mat-checkbox input')!;
    const alertas = () => [...elemento.querySelectorAll('[role="alert"]')].map((a) => a.textContent!.replace(/\s+/g, ' ').trim());
    const estado = () => elemento.querySelector('.progreso [role="status"]')!.textContent!.trim();
    const pulsarDescargar = async () => {
      boton('Descargar PDF')!.click();
      await refrescar();
    };
    return { fixture, elemento, refrescar, hasta, boton, elegir, casilla, alertas, estado, pulsarDescargar };
  }

  const peticion = (http: HttpTestingController): Promise<TestRequest> =>
    vi.waitFor(() => {
      const encontradas = http.match((r) => r.url === URL_DEL_PDF);
      if (encontradas.length === 0) {
        throw new Error('todavía no hay POST del PDF');
      }

      return encontradas.at(-1)!;
    });

  const problema = (cuerpo: unknown) => new Blob([JSON.stringify(cuerpo)], { type: 'application/problem+json' });
  const elPdf = () => new Blob(['%PDF-1.7'], { type: 'application/pdf' });
  const cabecera = (nombre: string) => ({ headers: { 'Content-Disposition': `attachment; filename="${nombre}"` } });

  it('abre con la cadena y los sellos marcados, sin logo y sin errores', () => {
    const { elemento, casilla, boton, alertas, estado } = abrir();

    expect(elemento.querySelector('h2')!.textContent).toContain('Descargar PDF');
    expect(casilla().checked).toBe(true);
    expect(boton('Elegir logo')).toBeDefined();
    expect(boton('Quitar logo')).toBeUndefined();
    expect(elemento.querySelector('img.vista')).toBeNull();
    expect(alertas()).toEqual([]);
    expect(estado()).toBe('');
    expect(boton('Cancelar')).toBeDefined();
  });

  it('un logo que no sirve se explica y no se queda elegido', async () => {
    const { elegir, alertas, boton, elemento } = abrir();

    await elegir(new File([new Uint8Array(10)], 'logo.gif', { type: 'image/gif' }));

    expect(alertas()).toEqual(['El logo debe ser una imagen PNG o JPEG.']);
    expect(boton('Quitar logo')).toBeUndefined();
    expect(elemento.querySelector('img.vista')).toBeNull();
  });

  it('un logo válido muestra su nombre y su vista previa, y elegir otro quita el aviso', async () => {
    const { elegir, alertas, boton, elemento, hasta } = abrir();

    await elegir(png('grande.gif.png', 205 * 1024));
    expect(alertas()).toEqual(['El logo pesa 205 KB; el máximo es 200 KB.']);

    await elegir(png('mi-logo.png'));
    await hasta(() => elemento.querySelector('img.vista'));

    expect(alertas()).toEqual([]);
    expect(elemento.querySelector('.nombre')!.textContent).toBe('mi-logo.png');
    expect(elemento.querySelector<HTMLImageElement>('img.vista')!.getAttribute('src')).toMatch(/^data:image\/png;base64,/);
    expect(elemento.querySelector('img.vista')!.getAttribute('alt')).toContain('Vista previa');
    expect(boton('Cambiar logo')).toBeDefined();
    expect(boton('Quitar logo')).toBeDefined();
  });

  it('quitar el logo lo borra y devuelve el foco al botón de elegir', async () => {
    const { elegir, boton, elemento, refrescar, hasta } = abrir();
    await elegir(png());
    await hasta(() => elemento.querySelector('img.vista'));

    boton('Quitar logo')!.click();
    await refrescar();

    expect(elemento.querySelector('img.vista')).toBeNull();
    expect(elemento.querySelector('.nombre')).toBeNull();
    expect(boton('Quitar logo')).toBeUndefined();
    expect(document.activeElement).toBe(boton('Elegir logo'));
  });

  it('descarga el PDF con el XML, el logo y las opciones, y cierra el diálogo con el nombre del archivo', async () => {
    const { ref, http, elegir, casilla, pulsarDescargar, estado, hasta, refrescar } = abrir();
    await elegir(png('mi-logo.png'));
    casilla().click();
    await refrescar();

    await pulsarDescargar();
    const llamada = await peticion(http);

    const formulario = llamada.request.body as FormData;
    expect(await (formulario.get('xml') as File).text()).toBe(XML);
    expect((formulario.get('logo') as File).name).toBe('mi-logo.png');
    expect(formulario.get('incluirCadenaYSellos')).toBe('false');
    expect(estado()).toBe('Dibujando el PDF…');
    expect(ref.disableClose).toBe(true);

    llamada.flush(elPdf(), cabecera('EKU9003173C9-A-123-prueba.pdf'));
    await hasta(() => ref.close.mock.calls.length > 0);

    expect(descargas).toEqual(['EKU9003173C9-A-123-prueba.pdf']);
    expect(ref.close).toHaveBeenCalledExactlyOnceWith({ nombre: 'EKU9003173C9-A-123-prueba.pdf' });
    expect(ref.disableClose).toBe(false);
    expect(estado()).toBe('');
  });

  it('sin logo no manda el campo, y la cadena y los sellos van por omisión', async () => {
    const { http, pulsarDescargar } = abrir();

    await pulsarDescargar();
    const llamada = await peticion(http);

    const formulario = llamada.request.body as FormData;
    expect(formulario.has('logo')).toBe(false);
    expect(formulario.get('incluirCadenaYSellos')).toBe('true');
    llamada.flush(elPdf());
  });

  it('mientras dibuja, el botón de descargar sigue en su lugar pero no lanza otra solicitud', async () => {
    const { http, boton, pulsarDescargar, elemento } = abrir();

    await pulsarDescargar();
    const llamada = await peticion(http);

    const descargar = boton('Descargar PDF')!;
    expect(descargar.getAttribute('aria-disabled')).toBe('true');
    expect(elemento.querySelector('mat-progress-bar')).not.toBeNull();
    descargar.click();
    expect(http.match((r) => r.url === URL_DEL_PDF)).toHaveLength(0);
    llamada.flush(elPdf());
  });

  it('un 400 con el error del logo lo pone junto al logo y deja el diálogo abierto para corregirlo', async () => {
    const { ref, http, elegir, pulsarDescargar, alertas, boton, refrescar, estado, elemento } = abrir();
    await elegir(png('enorme.png'));
    await pulsarDescargar();

    (await peticion(http)).flush(
      problema({ title: 'Hay datos que corregir', errors: { logo: ['El logo mide 5000 × 5000 px; el máximo es 2000 × 2000 px.'] } }),
      { status: 400, statusText: 'Bad Request' },
    );
    await refrescar();

    expect(alertas()).toEqual(['El logo mide 5000 × 5000 px; el máximo es 2000 × 2000 px.']);
    expect(elemento.querySelector('fieldset [role="alert"]')).not.toBeNull();
    expect(ref.close).not.toHaveBeenCalled();
    expect(ref.disableClose).toBe(false);
    expect(boton('Descargar PDF')!.getAttribute('aria-disabled')).not.toBe('true');
    expect(estado()).toBe('');
    expect(descargas).toEqual([]);
  });

  it('un 400 con el error del XML lo explica en el aviso del diálogo', async () => {
    const { http, pulsarDescargar, alertas, refrescar } = abrir();
    await pulsarDescargar();

    (await peticion(http)).flush(
      problema({ title: 'Hay datos que corregir', errors: { xml: ['El XML no se pudo leer (línea 3): falta cerrar la etiqueta.'] } }),
      { status: 400, statusText: 'Bad Request' },
    );
    await refrescar();

    expect(alertas()).toEqual(['El XML no se pudo dibujar. El XML no se pudo leer (línea 3): falta cerrar la etiqueta.']);
  });

  it('otros fallos (demasiadas solicitudes, un problema de la API, sin conexión) salen en el aviso', async () => {
    const { http, pulsarDescargar, alertas, refrescar } = abrir();

    await pulsarDescargar();
    (await peticion(http)).flush(
      problema({ title: 'Demasiadas solicitudes', detail: 'Se están dibujando otros PDF; espera unos segundos.' }),
      { status: 429, statusText: 'Too Many Requests' },
    );
    await refrescar();
    expect(alertas()).toEqual(['Demasiadas solicitudes. Se están dibujando otros PDF; espera unos segundos.']);

    await pulsarDescargar();
    expect(alertas()).toEqual([]); // el aviso anterior se quita al volver a intentar
    (await peticion(http)).flush(problema({ title: 'No se pudo dibujar el PDF', detail: 'El comprobante tiene demasiados datos.' }), {
      status: 422,
      statusText: 'Unprocessable Entity',
    });
    await refrescar();
    expect(alertas()).toEqual(['No se pudo dibujar el PDF. El comprobante tiene demasiados datos.']);

    await pulsarDescargar();
    (await peticion(http)).error(new ProgressEvent('error'), { status: 0, statusText: '' });
    await refrescar();
    expect(alertas()[0]).toContain('No se pudo conectar con la API.');
  });

  it('recuerda el logo y la casilla mientras la página siga abierta, pero solo después de descargar', async () => {
    const primero = abrir();
    await primero.elegir(png('mi-logo.png'));
    primero.casilla().click();
    await primero.refrescar();
    primero.fixture.destroy();
    expect(TestBed.inject(PreferenciasDelPdf).logo()).toBeNull(); // sin descargar no se recuerda nada

    const segundo = pintar();
    expect(segundo.casilla().checked).toBe(true);
    expect(segundo.boton('Elegir logo')).toBeDefined();
    await segundo.elegir(png('mi-logo.png'));
    segundo.casilla().click();
    await segundo.refrescar();
    await segundo.pulsarDescargar();
    const llamada = await peticion(TestBed.inject(HttpTestingController));
    llamada.flush(elPdf(), cabecera('a.pdf'));
    await segundo.hasta(() => TestBed.inject(PreferenciasDelPdf).logo() !== null);
    segundo.fixture.destroy();

    const tercero = pintar();
    await tercero.hasta(() => tercero.elemento.querySelector('img.vista'));
    expect(tercero.elemento.querySelector('.nombre')!.textContent).toBe('mi-logo.png');
    expect(tercero.casilla().checked).toBe(false);
    expect(tercero.boton('Cambiar logo')).toBeDefined();
  });
});

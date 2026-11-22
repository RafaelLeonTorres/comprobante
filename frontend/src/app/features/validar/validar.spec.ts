import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

import { registrarIconos } from '../../core/iconos';
import { provideIdioma } from '../../core/idioma';
import { ReporteValidacion } from '../../core/modelos';
import { TAMANO_MAXIMO, Validar } from './validar';

const XML = '<?xml version="1.0" encoding="utf-8"?>\n<cfdi:Comprobante Total="1161.00">\n  <cfdi:Receptor UsoCFDI="Z99"/>\n</cfdi:Comprobante>';

const REPORTE_CON_ERRORES: ReporteValidacion = {
  esValido: false,
  hallazgos: [
    { codigo: 'CFDI40119', severidad: 'error', nodo: 'cfdi:Comprobante', atributo: 'Total', mensaje: 'El Total no corresponde.', encontrado: '1161.00', esperado: '1160.00', linea: 2 },
    { codigo: 'CFDI40160', severidad: 'error', nodo: 'cfdi:Comprobante/cfdi:Receptor', atributo: 'UsoCFDI', mensaje: 'La clave Z99 no está en el catálogo.', encontrado: 'Z99', linea: 3 },
  ],
  cadenaOriginal: '||4.0||',
  selloValido: false,
  certificado: null,
};

const REPORTE_LIMPIO: ReporteValidacion = { esValido: true, hallazgos: [], cadenaOriginal: '||4.0||', selloValido: true, certificado: null };

describe('Validar', () => {
  function pintar() {
    TestBed.configureTestingModule({
      imports: [Validar],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideIdioma()],
    });
    registrarIconos(TestBed.inject(MatIconRegistry), TestBed.inject(DomSanitizer));
    HTMLElement.prototype.scrollIntoView = vi.fn();
    const fixture = TestBed.createComponent(Validar);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    const elemento = fixture.nativeElement as HTMLElement;

    /** Deja correr las promesas de la API (Angular no las sigue) y pinta. Solo con las peticiones ya respondidas. */
    const refrescar = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.tick();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    /** Espera (la lectura de un archivo no la sigue Angular) a que salga la petición a la API. */
    const peticion = async (url: string): Promise<TestRequest> =>
      vi.waitFor(() => {
        const encontradas = http.match(url);
        if (encontradas.length === 0) {
          throw new Error(`todavía no hay petición a ${url}`);
        }

        return encontradas[0]!;
      });
    const elegir = (archivo: File) => {
      const entrada = elemento.querySelector<HTMLInputElement>('input[type="file"]')!;
      Object.defineProperty(entrada, 'files', { value: [archivo], configurable: true });
      entrada.dispatchEvent(new Event('change'));
    };
    return { fixture, elemento, http, refrescar, peticion, elegir };
  }

  it('explica qué hace y ofrece archivo, pegar y ejemplos, sin ningún resultado todavía', () => {
    const { elemento } = pintar();

    expect(elemento.querySelector('h1')!.textContent).toContain('Validar un XML de CFDI 4.0');
    expect(elemento.querySelector('[aria-label="Zona para soltar un archivo XML"]')).not.toBeNull();
    expect(elemento.textContent).toContain('Elegir archivo');
    expect(elemento.querySelectorAll('.ejemplos button')).toHaveLength(2);
    expect(elemento.querySelector('.resultado')).toBeNull();
    expect(elemento.querySelector('app-cobertura-reglas')).not.toBeNull();
  });

  it('un archivo elegido se manda tal cual a la API y se muestra el reporte con el XML y sus líneas marcadas', async () => {
    const { elemento, refrescar, peticion, elegir } = pintar();

    elegir(new File([XML], 'factura.xml', { type: 'text/xml' }));
    const llamada = await peticion('/api/validaciones');

    expect(llamada.request.body).toBe(XML);
    expect(llamada.request.headers.get('Content-Type')).toBe('application/xml; charset=utf-8');
    llamada.flush(REPORTE_CON_ERRORES);
    await refrescar();

    expect(elemento.querySelector('#resultado-titulo')!.textContent).toContain('Resultado: factura.xml');
    expect(elemento.querySelector('app-panel-reporte [role="status"]')!.textContent).toContain('El XML tiene 2 errores');
    expect([...elemento.querySelectorAll('app-visor-xml .linea')].map((l) => l.textContent)).toEqual(XML.split('\n'));
    expect([...elemento.querySelectorAll('app-visor-xml .linea.con-hallazgo')].map((l) => l.getAttribute('data-linea'))).toEqual(['2', '3']);
  });

  it('el foco pasa al título del resultado', async () => {
    const { elemento, refrescar, peticion, elegir } = pintar();

    elegir(new File([XML], 'factura.xml'));
    (await peticion('/api/validaciones')).flush(REPORTE_LIMPIO);
    await refrescar();
    await vi.waitFor(() => expect(document.activeElement).toBe(elemento.querySelector('#resultado-titulo')));
  });

  it('el botón de una línea del reporte la lleva a la vista en el visor', async () => {
    const { elemento, refrescar, peticion, elegir } = pintar();
    elegir(new File([XML], 'factura.xml'));
    (await peticion('/api/validaciones')).flush(REPORTE_CON_ERRORES);
    await refrescar();

    elemento.querySelectorAll<HTMLButtonElement>('app-panel-reporte button.linea')[1]!.click();
    await refrescar();

    expect(elemento.querySelector('app-visor-xml .linea.activa')!.getAttribute('data-linea')).toBe('3');
    expect(HTMLElement.prototype.scrollIntoView).toHaveBeenCalled();
  });

  it('un archivo que pasa de 2 MB no se manda: se explica', async () => {
    const { elemento, http, refrescar, elegir } = pintar();

    elegir(new File([new Uint8Array(TAMANO_MAXIMO + 1)], 'enorme.xml'));
    await refrescar();

    const alerta = elemento.querySelector('[role="alert"]')!;
    expect(alerta.textContent).toContain('El archivo es demasiado grande');
    expect(alerta.textContent).toContain('enorme.xml pesa más de 2 MB');
    http.expectNone('/api/validaciones');
  });

  it('si la API rechaza el XML muestra su explicación y ningún resultado', async () => {
    const { elemento, refrescar, peticion, elegir } = pintar();

    elegir(new File([XML], 'factura.xml'));
    (await peticion('/api/validaciones')).flush(
      { title: 'Tipo de contenido no admitido', detail: 'Manda el XML como application/xml.' },
      { status: 415, statusText: 'Unsupported Media Type' },
    );
    await refrescar();

    expect(elemento.querySelector('[role="alert"]')!.textContent).toContain('Tipo de contenido no admitido');
    expect(elemento.querySelector('[role="alert"]')!.textContent).toContain('application/xml');
    expect(elemento.querySelector('.resultado')).toBeNull();
  });

  it('mientras valida avisa y bloquea los botones de ejemplo', async () => {
    const { elemento, refrescar, peticion, elegir } = pintar();

    elegir(new File([XML], 'factura.xml'));
    const llamada = await peticion('/api/validaciones');
    await refrescar();

    expect(elemento.querySelector('.progreso[role="status"]')!.textContent).toContain('Validando');
    expect([...elemento.querySelectorAll<HTMLButtonElement>('.ejemplos button')].every((b) => b.disabled)).toBe(true);

    llamada.flush(REPORTE_LIMPIO);
    await refrescar();
    expect(elemento.querySelector('.progreso')).toBeNull();
  });

  it('el XML pegado se valida al enviar el formulario', async () => {
    const { elemento, refrescar, peticion } = pintar();
    const campo = elemento.querySelector<HTMLTextAreaElement>('textarea')!;

    campo.value = XML;
    campo.dispatchEvent(new Event('input'));
    elemento.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    const llamada = await peticion('/api/validaciones');

    expect(llamada.request.body).toBe(XML);
    llamada.flush(REPORTE_LIMPIO);
    await refrescar();
    expect(elemento.querySelector('#resultado-titulo')!.textContent).toContain('Resultado: XML pegado');
    expect(elemento.querySelector('app-panel-reporte [role="status"]')!.textContent).toContain('Sin hallazgos');
  });

  it('enviar el formulario vacío pide el XML y no llama a la API', async () => {
    const { elemento, http, refrescar } = pintar();

    elemento.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await refrescar();

    expect(elemento.querySelector('mat-error')!.textContent).toContain('Pega el XML que quieres validar.');
    http.expectNone('/api/validaciones');
  });

  it('un ejemplo se baja de la app y se valida', async () => {
    const { elemento, http, refrescar, peticion } = pintar();

    elemento.querySelectorAll<HTMLButtonElement>('.ejemplos button')[0]!.click();
    const ejemplo = http.expectOne('/ejemplos/ingreso-valido.xml');
    ejemplo.flush(XML);
    const llamada = await peticion('/api/validaciones');
    expect(llamada.request.body).toBe(XML);
    llamada.flush(REPORTE_LIMPIO);
    await refrescar();

    expect(elemento.querySelector('#resultado-titulo')!.textContent).toContain('Resultado: Un CFDI sin errores');
  });

  it('soltar un archivo en la zona lo valida igual que elegirlo', async () => {
    const { elemento, refrescar, peticion } = pintar();
    const zona = elemento.querySelector<HTMLElement>('.zona')!;

    const encima = new Event('dragover', { cancelable: true });
    zona.dispatchEvent(encima);
    await refrescar();
    expect(encima.defaultPrevented).toBe(true);
    expect(zona.classList).toContain('arrastrando');

    const soltar = new Event('drop', { cancelable: true });
    Object.defineProperty(soltar, 'dataTransfer', { value: { files: [new File([XML], 'arrastrado.xml')] } });
    zona.dispatchEvent(soltar);
    const llamada = await peticion('/api/validaciones');
    llamada.flush(REPORTE_LIMPIO);
    await refrescar();

    expect(zona.classList).not.toContain('arrastrando');
    expect(elemento.querySelector('#resultado-titulo')!.textContent).toContain('Resultado: arrastrado.xml');
  });

  it('con un XML sin errores ofrece descargar su PDF', async () => {
    const { elemento, refrescar, peticion, elegir } = pintar();
    elegir(new File([XML], 'factura.xml'));
    (await peticion('/api/validaciones')).flush(REPORTE_LIMPIO);
    await refrescar();

    const boton = elemento.querySelector<HTMLButtonElement>('.resultado app-boton-pdf button')!;
    expect(boton.textContent).toContain('Descargar PDF');
    expect(boton.getAttribute('aria-disabled')).not.toBe('true');
    expect(elemento.querySelector('.resultado app-boton-pdf .nota:not([role])')).toBeNull();
  });

  it('con errores el botón del PDF se queda a la vista, deshabilitado y con el motivo', async () => {
    const { elemento, refrescar, peticion, elegir } = pintar();
    elegir(new File([XML], 'factura.xml'));
    (await peticion('/api/validaciones')).flush(REPORTE_CON_ERRORES);
    await refrescar();

    const boton = elemento.querySelector<HTMLButtonElement>('.resultado app-boton-pdf button')!;
    expect(boton.getAttribute('aria-disabled')).toBe('true');
    expect(elemento.querySelector('.resultado app-boton-pdf .nota:not([role])')!.textContent).toBe('El PDF se habilita cuando el XML no tiene errores.');
  });
});

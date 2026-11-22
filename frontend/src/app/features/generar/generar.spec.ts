import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

import { registrarIconos } from '../../core/iconos';
import { provideIdioma } from '../../core/idioma';
import { EmisorDePrueba, ReporteValidacion, ResultadoCalculo, ResultadoGeneracion } from '../../core/modelos';
import { Generar } from './generar';

const EMISORES: EmisorDePrueba[] = [
  {
    rfc: 'EKU9003173C9',
    nombre: 'ESCUELA KEMPER URGATE',
    persona: 'moral',
    codigoPostal: '42501',
    regimenFiscal: '601',
    noCertificado: '30001000000500003416',
    validoDesde: '2023-05-18T14:00:00Z',
    validoHasta: '2027-05-18T14:00:00Z',
  },
  {
    rfc: 'WATM640917J45',
    nombre: 'MARIA OLIVIA MARTINEZ SAGAZ',
    persona: 'fisica',
    codigoPostal: '43543',
    regimenFiscal: '612',
    noCertificado: '30001000000500003417',
    validoDesde: '2023-05-18T14:00:00Z',
    validoHasta: '2027-05-18T14:00:00Z',
  },
];

// Como la responde la API: lo que es nulo (sin descuento, sin retenciones) no viene.
const CALCULO: ResultadoCalculo = {
  moneda: 'MXN',
  decimalesMoneda: 2,
  conceptos: [{ importe: 1000, traslados: [{ impuesto: '002', tipoFactor: 'Tasa', tasaOCuota: 0.16, base: 1000, importe: 160 }], retenciones: [] }],
  subTotal: 1000,
  traslados: [{ base: 1000, impuesto: '002', tipoFactor: 'Tasa', tasaOCuota: 0.16, importe: 160 }],
  retenciones: [],
  totalImpuestosTrasladados: 160,
  total: 1160,
};

const XML = '<?xml version="1.0" encoding="utf-8"?>\n<cfdi:Comprobante Total="1160.00">\n  <cfdi:Emisor Rfc="EKU9003173C9"/>\n</cfdi:Comprobante>';

const VALIDACION_LIMPIA: ReporteValidacion = { esValido: true, hallazgos: [], cadenaOriginal: '||4.0||', selloValido: true, certificado: null };

const GENERACION: ResultadoGeneracion = {
  xml: XML,
  nombreArchivo: 'EKU9003173C9-A-123-prueba.xml',
  cadenaOriginal: '||4.0||',
  sello: 'c2VsbG8=',
  noCertificado: '30001000000500003416',
  calculo: CALCULO,
  validacion: VALIDACION_LIMPIA,
};

describe('Generar', () => {
  function pintar() {
    TestBed.configureTestingModule({
      imports: [Generar],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideIdioma()],
    });
    registrarIconos(TestBed.inject(MatIconRegistry), TestBed.inject(DomSanitizer));
    HTMLElement.prototype.scrollIntoView = vi.fn();
    const fixture = TestBed.createComponent(Generar);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    const elemento = fixture.nativeElement as HTMLElement;

    /**
     * Deja correr las promesas y los retrasos cortos y pinta. No espera a `whenStable()`: la lista de emisores y los totales
     * son peticiones HTTP que la prueba mantiene abiertas a propósito, y mientras haya una Angular no se da por estable.
     */
    const refrescar = async (espera = 0) => {
      await new Promise((resolve) => setTimeout(resolve, espera));
      TestBed.tick();
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.tick();
      fixture.detectChanges();
    };
    const peticion = (url: string, metodo = 'POST'): Promise<TestRequest> =>
      vi.waitFor(() => {
        const encontradas = http.match((r) => r.url === url && r.method === metodo);
        if (encontradas.length === 0) {
          throw new Error(`todavía no hay ${metodo} ${url}`);
        }

        return encontradas.at(-1)!;
      });
    const emisores = async () => {
      (await peticion('/api/emisores-prueba', 'GET')).flush(EMISORES);
      await refrescar();
    };
    const ejemplo = async (titulo: string) => {
      // Un usuario no puede pulsar en la misma tarea que pintó la página: los selectores de Material terminan de iniciarse
      // en una microtarea y leen su campo, que el ejemplo reemplazaría.
      await refrescar();
      const boton = [...elemento.querySelectorAll<HTMLButtonElement>('.ejemplos button')].find((b) => b.textContent!.includes(titulo))!;
      boton.click();
      await refrescar();
    };
    const campos = (etiqueta: string) =>
      [...elemento.querySelectorAll('mat-form-field')]
        .filter((f) => f.querySelector('mat-label')?.textContent?.trim() === etiqueta)
        .map((f) => f.querySelector<HTMLInputElement>('input, textarea')!);
    const campo = (etiqueta: string) => campos(etiqueta)[0]!;
    const escribir = async (entrada: HTMLInputElement | HTMLTextAreaElement, valor: string) => {
      entrada.value = valor;
      entrada.dispatchEvent(new Event('input'));
      await refrescar();
    };
    const enviar = async () => {
      elemento.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
      await refrescar();
    };
    const conceptos = () => elemento.querySelectorAll('app-tarjeta-concepto');
    return { fixture, elemento, http, refrescar, peticion, emisores, ejemplo, campo, campos, escribir, enviar, conceptos };
  }

  afterEach(() => {
    document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = ''));
  });

  it('abre con el formulario en blanco, un concepto, los cinco ejemplos y sin regañar todavía', async () => {
    const { elemento, refrescar, conceptos } = pintar();
    await refrescar();

    expect(elemento.querySelector('h1')!.textContent).toContain('Generar un CFDI 4.0');
    expect(elemento.querySelectorAll('.ejemplos button')).toHaveLength(5);
    expect(conceptos()).toHaveLength(1);
    expect(elemento.querySelector('.problemas')).toBeNull();
    expect(elemento.querySelector('app-totales-calculo')!.textContent).toContain('Agrega un concepto con cantidad y valor unitario');
    expect(elemento.querySelector('#resultado-titulo')).toBeNull();
  });

  it('un ejemplo llena el formulario y lo dice', async () => {
    const { elemento, ejemplo, campo, emisores } = pintar();
    await emisores();

    await ejemplo('Un servicio con IVA');

    expect(campo('RFC').value).toBe('XIA190128J61');
    expect(campo('Nombre o razón social').value).toBe('XENON INDUSTRIAL ARTICLES');
    expect(campo('Descripción').value).toBe('Servicio de facturación electrónica');
    expect(campo('Cantidad').value).toBe('1');
    expect(campo('Valor unitario').value).toBe('1000');
    expect(elemento.querySelector('.aviso-ejemplo')!.textContent).toContain('Se cargó «Un servicio con IVA»');
    expect(elemento.querySelector('.datos')!.textContent).toContain('30001000000500003416');
  });

  it('el ejemplo con tres conceptos muestra tres tarjetas, con sus impuestos y retenciones', async () => {
    const { ejemplo, conceptos, elemento } = pintar();

    await ejemplo('Con retenciones y descuento');

    expect(conceptos()).toHaveLength(3);
    const primero = conceptos()[0]!;
    expect(primero.querySelectorAll('.fila')).toHaveLength(3);
    expect(elemento.textContent).toContain('Sin tasa ni cuota');
  });

  it('con un concepto calculable pide los totales a la API sin mandar fecha ni campos vacíos y los muestra', async () => {
    const { elemento, ejemplo, peticion, refrescar } = pintar();

    await ejemplo('Un servicio con IVA');
    const llamada = await peticion('/api/comprobantes/calculo');

    const cuerpo = JSON.parse(JSON.stringify(llamada.request.body));
    expect(cuerpo).toMatchObject({
      emisor: { rfc: 'EKU9003173C9' },
      moneda: 'MXN',
      tipoDeComprobante: 'I',
      conceptos: [{ claveProdServ: '84111506', cantidad: 1, valorUnitario: 1000, traslados: [{ impuesto: '002', tipoFactor: 'Tasa', tasaOCuota: 0.16 }] }],
    });
    expect(cuerpo).not.toHaveProperty('fecha');
    expect(cuerpo).not.toHaveProperty('tipoCambio');
    expect(cuerpo).not.toHaveProperty('informacionGlobal');
    expect(cuerpo).not.toHaveProperty('cfdiRelacionados');
    llamada.flush(CALCULO);
    await refrescar();

    const limpio = (nodo: Element | null) => nodo!.textContent!.replace(/\s+/g, ' ').trim();
    const filas = new Map(
      [...elemento.querySelectorAll('app-totales-calculo dl > div')].map((fila) => [limpio(fila.querySelector('dt')), limpio(fila.querySelector('dd'))]),
    );
    expect(Object.fromEntries(filas)).toEqual({
      Subtotal: '1,000.00',
      'IVA 16 % trasladado base 1,000.00': '160.00',
      Total: '1,160.00 MXN',
    });
    expect(elemento.querySelector('app-tarjeta-concepto .importe')!.textContent).toContain('1,000.00');
  });

  it('si la API no puede calcular, lo dice con su primer mensaje en lugar de los totales', async () => {
    const { elemento, ejemplo, peticion, refrescar } = pintar();

    await ejemplo('Un servicio con IVA');
    (await peticion('/api/comprobantes/calculo')).flush(
      { title: 'Hay datos que corregir', errors: { 'conceptos[0].descuento': ['El descuento no puede ser mayor que el importe del concepto.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await refrescar();

    const totales = elemento.querySelector('app-totales-calculo')!.textContent!;
    expect(totales).toContain('Todavía no se pueden calcular los totales.');
    expect(totales).toContain('El descuento no puede ser mayor que el importe del concepto.');
  });

  it('con una moneda extranjera pide el tipo de cambio y con MXN lo oculta', async () => {
    const { ejemplo, campo, campos, escribir, refrescar } = pintar();

    await ejemplo('En dólares');
    expect(campo('Moneda').value).toBe('USD');
    expect(campo('Tipo de cambio').value).toBe('17.4521');

    await escribir(campo('Moneda'), 'MXN');
    await refrescar(50);

    expect(campos('Tipo de cambio')).toHaveLength(0);
  });

  it('el público en general muestra la información global con el mes en curso', async () => {
    const { elemento, ejemplo, campo, escribir, refrescar } = pintar();
    await ejemplo('Público en general');

    expect(elemento.querySelector('section[aria-labelledby="s-global"]')).not.toBeNull();
    expect(campo('Año').value).toBe(String(new Date().getFullYear()));

    await escribir(campo('Nombre o razón social'), 'OTRA PERSONA');
    await refrescar();
    expect(elemento.querySelector('section[aria-labelledby="s-global"]')).toBeNull();

    await escribir(campo('Nombre o razón social'), 'Publico en general');
    await refrescar();
    expect(elemento.querySelector('section[aria-labelledby="s-global"]')).not.toBeNull();
  });

  it('la casilla de CFDI relacionados muestra y oculta el tipo de relación y los folios', async () => {
    const { elemento, ejemplo, campo, campos, refrescar } = pintar();
    await ejemplo('Egreso con CFDI relacionado');

    expect(campo('Folios fiscales (UUID)').value).toBe('5FB2822E-396D-4725-8521-CDC4BDD20CCF');
    expect(campos('Tipo de relación')).toHaveLength(1);

    const casilla = elemento.querySelector<HTMLInputElement>('section[aria-labelledby="s-relacionados"] mat-checkbox input')!;
    casilla.click();
    await refrescar();
    expect(campos('Folios fiscales (UUID)')).toHaveLength(0);
    expect(campos('Tipo de relación')).toHaveLength(0);

    casilla.click();
    await refrescar();
    expect(campos('Folios fiscales (UUID)')).toHaveLength(1);
  });

  it('agrega y quita conceptos, y el último no se puede quitar', async () => {
    const { elemento, ejemplo, conceptos, refrescar } = pintar();
    await ejemplo('Un servicio con IVA');
    const botonesDeQuitar = () =>
      [...elemento.querySelectorAll<HTMLButtonElement>('app-tarjeta-concepto header button')].filter((b) => b.textContent!.includes('Quitar concepto'));
    expect(botonesDeQuitar()).toHaveLength(0);

    elemento.querySelector<HTMLButtonElement>('#agregar-concepto')!.click();
    await refrescar();
    expect(conceptos()).toHaveLength(2);
    expect(conceptos()[1]!.querySelector('h3')!.textContent).toContain('Concepto 2');
    expect(botonesDeQuitar()).toHaveLength(2);

    botonesDeQuitar()[0]!.click();
    await refrescar();
    expect(conceptos()).toHaveLength(1);
    expect(elemento.querySelector('app-tarjeta-concepto h3')!.textContent).toContain('Concepto 1');
    expect(botonesDeQuitar()).toHaveLength(0);
  });

  it('agrega y quita impuestos de un concepto', async () => {
    const { elemento, ejemplo, conceptos, refrescar } = pintar();
    await ejemplo('Un servicio con IVA');
    const concepto = () => conceptos()[0]!;
    expect(concepto().querySelectorAll('.fila')).toHaveLength(1);

    const agregarRetencion = [...concepto().querySelectorAll<HTMLButtonElement>('.encabezado button')].find((b) => b.textContent!.includes('Agregar retención'))!;
    agregarRetencion.click();
    await refrescar();
    expect(concepto().querySelectorAll('.fila')).toHaveLength(2);
    expect(elemento.querySelector('[aria-label="Retención 1"]')).not.toBeNull();

    concepto().querySelector<HTMLButtonElement>('[aria-label="Quitar Retención 1"]')!.click();
    await refrescar();
    expect(concepto().querySelectorAll('.fila')).toHaveLength(1);
  });

  it('con el formulario incompleto no llama a la API: lista lo que falta con su campo y lleva el foco al aviso', async () => {
    const { elemento, http, enviar } = pintar();

    await enviar();

    http.expectNone('/api/comprobantes');
    const aviso = elemento.querySelector('.problemas')!;
    expect(aviso.querySelector('h2')!.textContent).toContain('Hay datos que corregir');
    const items = [...aviso.querySelectorAll('li')].map((li) => li.textContent!.replace(/\s+/g, ' ').trim());
    expect(items).toEqual(
      expect.arrayContaining([
        'Emisor: Elige el emisor de prueba con el que se sella.',
        'Receptor › RFC: Escribe el RFC del receptor.',
        'Concepto 1 › Producto o servicio: Escribe la clave de producto o servicio.',
        'Concepto 1 › Valor unitario: Escribe el valor unitario.',
      ]),
    );
    await vi.waitFor(() => expect(document.activeElement).toBe(aviso.querySelector('h2')));
    expect(elemento.querySelector('mat-error')).not.toBeNull();
  });

  it('un formulario correcto se manda a la API y se muestra el XML con su reporte', async () => {
    const { elemento, ejemplo, enviar, peticion, refrescar } = pintar();
    await ejemplo('Un servicio con IVA');

    await enviar();
    const llamada = await peticion('/api/comprobantes');

    expect(llamada.request.body).toMatchObject({ serie: 'A', folio: '123', emisor: { rfc: 'EKU9003173C9' }, receptor: { rfc: 'XIA190128J61', usoCfdi: 'G03' } });
    expect(elemento.querySelector('.progreso')!.textContent).toContain('Generando y sellando');
    expect((elemento.querySelector('button.generar') as HTMLButtonElement).disabled).toBe(true);
    llamada.flush(GENERACION);
    await refrescar();

    expect(elemento.querySelector('#resultado-titulo')!.textContent).toContain('CFDI generado');
    expect(elemento.querySelector('.resumen-resultado')!.textContent).toContain('El validador no encontró hallazgos.');
    expect(elemento.querySelector('.resumen-resultado')!.textContent).toContain('EKU9003173C9-A-123-prueba.xml');
    expect([...elemento.querySelectorAll('app-visor-xml .linea')].map((l) => l.textContent)).toEqual(XML.split('\n'));
    expect(elemento.querySelector('.problemas')).toBeNull();
    await vi.waitFor(() => expect(document.activeElement).toBe(elemento.querySelector('#resultado-titulo')));
  });

  it('descarga el XML con el nombre que dio la API', async () => {
    const { elemento, ejemplo, enviar, peticion, refrescar } = pintar();
    URL.createObjectURL = vi.fn(() => 'blob:prueba');
    URL.revokeObjectURL = vi.fn();
    const descargas: string[] = [];
    const clic = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      descargas.push(this.download);
    });
    await ejemplo('Un servicio con IVA');
    await enviar();
    (await peticion('/api/comprobantes')).flush(GENERACION);
    await refrescar();

    [...elemento.querySelectorAll<HTMLButtonElement>('.resultado button')].find((b) => b.textContent!.includes('Descargar XML'))!.click();

    expect(descargas).toEqual(['EKU9003173C9-A-123-prueba.xml']);
    clic.mockRestore();
  });

  it('con un XML sin hallazgos ofrece descargar el PDF', async () => {
    const { elemento, ejemplo, enviar, peticion, refrescar } = pintar();
    await ejemplo('Un servicio con IVA');
    await enviar();
    (await peticion('/api/comprobantes')).flush(GENERACION);
    await refrescar();

    const boton = elemento.querySelector<HTMLButtonElement>('.resultado app-boton-pdf button')!;
    expect(boton.textContent).toContain('Descargar PDF');
    expect(boton.getAttribute('aria-disabled')).not.toBe('true');
    expect(elemento.querySelector('.resultado app-boton-pdf .nota:not([role])')).toBeNull();
  });

  it('con hallazgos el botón del PDF se queda a la vista, deshabilitado y con el motivo', async () => {
    const { elemento, ejemplo, enviar, peticion, refrescar } = pintar();
    await ejemplo('Un servicio con IVA');
    await enviar();
    const conErrores: ReporteValidacion = {
      ...VALIDACION_LIMPIA,
      esValido: false,
      hallazgos: [{ codigo: 'CFDI40119', severidad: 'error', nodo: 'cfdi:Comprobante', atributo: 'Total', mensaje: 'El Total no corresponde.', linea: 2 }],
    };
    (await peticion('/api/comprobantes')).flush({ ...GENERACION, validacion: conErrores });
    await refrescar();

    const boton = elemento.querySelector<HTMLButtonElement>('.resultado app-boton-pdf button')!;
    expect(boton.getAttribute('aria-disabled')).toBe('true');
    expect(elemento.querySelector('.resultado app-boton-pdf .nota:not([role])')!.textContent).toBe('El PDF se habilita cuando el XML no tiene errores.');
  });

  it('el botón del PDF abre el diálogo y manda a la API el XML generado', async () => {
    const { elemento, ejemplo, enviar, peticion, refrescar } = pintar();
    const original = { crear: URL.createObjectURL, soltar: URL.revokeObjectURL };
    URL.createObjectURL = vi.fn(() => 'blob:prueba');
    URL.revokeObjectURL = vi.fn();
    const clic = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    await ejemplo('Un servicio con IVA');
    await enviar();
    (await peticion('/api/comprobantes')).flush(GENERACION);
    await refrescar();

    elemento.querySelector<HTMLButtonElement>('.resultado app-boton-pdf button')!.click();
    await refrescar();
    const dialogo = document.querySelector('mat-dialog-container')!;
    expect(dialogo.querySelector('h2')!.textContent).toContain('Descargar PDF');
    [...dialogo.querySelectorAll<HTMLButtonElement>('button')].find((b) => b.textContent!.includes('Descargar PDF'))!.click();
    const llamada = await peticion('/api/representaciones/pdf');

    const formulario = llamada.request.body as FormData;
    expect(await (formulario.get('xml') as File).text()).toBe(XML);
    expect(formulario.get('incluirCadenaYSellos')).toBe('true');
    llamada.flush(new Blob(['%PDF-1.7'], { type: 'application/pdf' }), { headers: { 'Content-Disposition': 'attachment; filename="EKU9003173C9-A-123-prueba.pdf"' } });
    await refrescar();

    clic.mockRestore();
    URL.createObjectURL = original.crear;
    URL.revokeObjectURL = original.soltar;
  });

  it('un 400 de la API pone cada error en su campo y los que no tienen campo, en el aviso', async () => {
    const { elemento, ejemplo, enviar, peticion, refrescar, campo } = pintar();
    await ejemplo('Un servicio con IVA');
    await enviar();

    (await peticion('/api/comprobantes')).flush(
      {
        title: 'Hay datos que corregir',
        detail: 'Revisa los campos marcados y vuelve a intentarlo.',
        errors: {
          'conceptos[0].cantidad': ['La cantidad admite hasta 6 decimales.'],
          moneda: ['La moneda ZZZ no está en el catálogo c_Moneda.'],
          '': ['Algo general del comprobante.'],
        },
      },
      { status: 400, statusText: 'Bad Request' },
    );
    await refrescar();

    const items = [...elemento.querySelectorAll('.problemas li')].map((li) => li.textContent!.replace(/\s+/g, ' ').trim());
    expect(items).toEqual(
      expect.arrayContaining([
        'Algo general del comprobante.',
        'Concepto 1 › Cantidad: La cantidad admite hasta 6 decimales.',
        'Moneda: La moneda ZZZ no está en el catálogo c_Moneda.',
      ]),
    );
    expect(elemento.querySelector('.resultado')).toBeNull();
    const errorDeCantidad = campo('Cantidad').closest('mat-form-field')!.querySelector('mat-error');
    expect(errorDeCantidad?.textContent).toContain('La cantidad admite hasta 6 decimales.');
  });

  it('quien corrige el campo hace desaparecer su error del aviso', async () => {
    const { elemento, ejemplo, enviar, peticion, refrescar, campo, escribir } = pintar();
    await ejemplo('Un servicio con IVA');
    await enviar();
    (await peticion('/api/comprobantes')).flush(
      { title: 'Hay datos que corregir', errors: { 'conceptos[0].cantidad': ['La cantidad admite hasta 6 decimales.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await refrescar();
    expect(elemento.querySelectorAll('.problemas li')).toHaveLength(1);

    await escribir(campo('Cantidad'), '2');
    await refrescar();

    expect(elemento.querySelector('.problemas')).toBeNull();
  });

  it('un fallo sin campo (la API caída) sale en el aviso con su explicación y sin tocar los campos', async () => {
    const { elemento, ejemplo, enviar, peticion, refrescar } = pintar();
    await ejemplo('Un servicio con IVA');
    await enviar();

    (await peticion('/api/comprobantes')).error(new ProgressEvent('error'), { status: 0, statusText: 'Unknown Error' });
    await refrescar();

    const aviso = elemento.querySelector('.problemas')!;
    expect(aviso.textContent).toContain('No se pudo conectar con la API');
    expect(aviso.textContent).toContain('docker compose ps');
    expect(aviso.querySelectorAll('li')).toHaveLength(0);
    expect(elemento.querySelector('.resultado')).toBeNull();
  });

  it('si fallan los emisores de prueba lo dice', async () => {
    const { elemento, peticion, refrescar } = pintar();

    (await peticion('/api/emisores-prueba', 'GET')).flush(null, { status: 500, statusText: 'Error' });
    await refrescar();

    expect(elemento.querySelector('section[aria-labelledby="s-emisor"] [role="alert"]')!.textContent).toContain('No se pudo cargar la lista de emisores de prueba');
  });
});

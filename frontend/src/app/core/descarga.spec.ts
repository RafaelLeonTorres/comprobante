import { descargarArchivo, descargarTexto, nombreDeDescarga } from './descarga';

describe('nombreDeDescarga', () => {
  it('toma el nombre entre comillas', () => {
    expect(nombreDeDescarga('attachment; filename="EKU9003173C9-A-123-prueba.pdf"')).toBe('EKU9003173C9-A-123-prueba.pdf');
  });

  it('toma el nombre sin comillas, hasta el punto y coma', () => {
    expect(nombreDeDescarga('attachment; filename=factura.pdf; size=100')).toBe('factura.pdf');
  });

  it('prefiere la forma extendida, que lleva los acentos y los espacios', () => {
    expect(nombreDeDescarga(`attachment; filename="Factura.pdf"; filename*=UTF-8''Factura%20Ni%C3%B1o.pdf`)).toBe('Factura Niño.pdf');
  });

  it('si la forma extendida está mal escrita, usa la simple', () => {
    expect(nombreDeDescarga(`attachment; filename="respaldo.pdf"; filename*=UTF-8''100%.pdf`)).toBe('respaldo.pdf');
  });

  it('sin cabecera, o sin nombre en ella, propone el nombre por omisión', () => {
    expect(nombreDeDescarga(null)).toBe('comprobante.pdf');
    expect(nombreDeDescarga('attachment')).toBe('comprobante.pdf');
    expect(nombreDeDescarga('attachment', 'otro.pdf')).toBe('otro.pdf');
  });
});

describe('descargarArchivo', () => {
  const original = { crear: URL.createObjectURL, soltar: URL.revokeObjectURL };
  let creados: Blob[];
  let soltados: string[];
  let enlaces: { href: string; download: string; rel: string; ligado: boolean }[];

  beforeEach(() => {
    vi.useFakeTimers();
    creados = [];
    soltados = [];
    enlaces = [];
    // jsdom no implementa las URL de objetos.
    URL.createObjectURL = vi.fn((archivo: Blob | MediaSource) => {
      creados.push(archivo as Blob);
      return 'blob:prueba';
    });
    URL.revokeObjectURL = vi.fn((url: string) => {
      soltados.push(url);
    });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      enlaces.push({ href: this.href, download: this.download, rel: this.rel, ligado: this.isConnected });
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
    URL.createObjectURL = original.crear;
    URL.revokeObjectURL = original.soltar;
  });

  it('pulsa un enlace con el nombre del archivo, lo quita de la página y suelta la URL después', () => {
    const pdf = new Blob(['%PDF-1.7'], { type: 'application/pdf' });

    descargarArchivo('factura.pdf', pdf);

    expect(creados).toEqual([pdf]);
    expect(enlaces).toEqual([{ href: 'blob:prueba', download: 'factura.pdf', rel: 'noopener', ligado: true }]);
    expect(document.querySelector('a[download]')).toBeNull();
    expect(soltados).toEqual([]);

    vi.advanceTimersByTime(10_000);
    expect(soltados).toEqual(['blob:prueba']);
  });

  it('un texto se descarga como un archivo con su tipo y la codificación', async () => {
    descargarTexto('comprobante.xml', '<a>ñ</a>');

    expect(enlaces).toHaveLength(1);
    expect(enlaces[0]!.download).toBe('comprobante.xml');
    expect(creados[0]!.type).toBe('application/xml;charset=utf-8');
    expect(await creados[0]!.text()).toBe('<a>ñ</a>');
  });
});

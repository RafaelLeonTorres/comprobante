import { TestBed } from '@angular/core/testing';

import { TemaColor } from './tema';

describe('TemaColor', () => {
  const ventana = document.defaultView!;
  let sistemaOscuro: boolean;
  let avisarCambio: (oscuro: boolean) => void;

  beforeEach(() => {
    ventana.localStorage.clear();
    delete document.documentElement.dataset['tema'];
    sistemaOscuro = false;
    avisarCambio = () => {};
    // jsdom no trae matchMedia: este simula la preferencia del sistema y el aviso de que cambió.
    Object.defineProperty(ventana, 'matchMedia', {
      configurable: true,
      writable: true,
      value: () => ({
        get matches() {
          return sistemaOscuro;
        },
        addEventListener: (_: string, oyente: (e: { matches: boolean }) => void) => {
          avisarCambio = (oscuro) => {
            sistemaOscuro = oscuro;
            oyente({ matches: oscuro });
          };
        },
      }),
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    delete (ventana as unknown as { matchMedia?: unknown }).matchMedia;
  });

  it('sin elección sigue al sistema y no toca <html>', () => {
    const tema = TestBed.inject(TemaColor);

    expect(tema.actual()).toBe('claro');
    expect(document.documentElement.dataset['tema']).toBeUndefined();

    avisarCambio(true);

    expect(tema.actual()).toBe('oscuro');
    expect(document.documentElement.dataset['tema']).toBeUndefined();
  });

  it('empieza en oscuro si el sistema lo prefiere', () => {
    sistemaOscuro = true;

    expect(TestBed.inject(TemaColor).actual()).toBe('oscuro');
  });

  it('usa el tema guardado, aunque el sistema prefiera el otro, y lo pone en <html>', () => {
    ventana.localStorage.setItem('comprobante.tema', 'oscuro');

    const tema = TestBed.inject(TemaColor);

    expect(tema.actual()).toBe('oscuro');
    expect(document.documentElement.dataset['tema']).toBe('oscuro');
  });

  it('ignora un valor guardado que no es un tema', () => {
    ventana.localStorage.setItem('comprobante.tema', 'rosa');

    const tema = TestBed.inject(TemaColor);

    expect(tema.actual()).toBe('claro');
    expect(document.documentElement.dataset['tema']).toBeUndefined();
  });

  it('alternar cambia al otro tema, lo guarda y lo pone en <html>; una elección ya no sigue al sistema', () => {
    const tema = TestBed.inject(TemaColor);

    tema.alternar();

    expect(tema.actual()).toBe('oscuro');
    expect(ventana.localStorage.getItem('comprobante.tema')).toBe('oscuro');
    expect(document.documentElement.dataset['tema']).toBe('oscuro');

    avisarCambio(false);
    expect(tema.actual()).toBe('oscuro');

    tema.alternar();

    expect(tema.actual()).toBe('claro');
    expect(ventana.localStorage.getItem('comprobante.tema')).toBe('claro');
    expect(document.documentElement.dataset['tema']).toBe('claro');
  });

  it('con el almacenamiento bloqueado sigue funcionando', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('bloqueado');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('bloqueado');
    });

    const tema = TestBed.inject(TemaColor);
    tema.alternar();

    expect(tema.actual()).toBe('oscuro');
    expect(document.documentElement.dataset['tema']).toBe('oscuro');
  });
});

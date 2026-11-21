import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { conUltimoValor, retrasada } from './senales';

describe('retrasada', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  function crear<T>(inicial: T, milisegundos = 100) {
    const fuente = signal(inicial);
    const salida = TestBed.runInInjectionContext(() => retrasada(fuente, milisegundos));
    TestBed.tick();
    return { fuente, salida };
  }

  it('empieza con el valor que tiene la fuente', () => {
    const { salida } = crear('G03');

    expect(salida()).toBe('G03');
  });

  it('un cambio no se ve hasta que pasa el retraso', () => {
    const { fuente, salida } = crear('a');

    fuente.set('b');
    TestBed.tick();
    vi.advanceTimersByTime(99);
    expect(salida()).toBe('a');

    vi.advanceTimersByTime(1);
    expect(salida()).toBe('b');
  });

  it('varios cambios seguidos se reducen al último', () => {
    const { fuente, salida } = crear('');
    const vistos: string[] = [];

    for (const texto of ['g', 'g0', 'g03']) {
      fuente.set(texto);
      TestBed.tick();
      vi.advanceTimersByTime(60);
      vistos.push(salida());
    }

    expect(vistos).toEqual(['', '', '']);
    vi.advanceTimersByTime(100);
    expect(salida()).toBe('g03');
  });

  it('se puede crear antes de que la fuente tenga valor definitivo y lee el que haya al usarla', () => {
    const fuente = signal('antes');
    const salida = TestBed.runInInjectionContext(() => retrasada(fuente, 100));
    fuente.set('despues');

    expect(salida()).toBe('despues');
  });
});

describe('conUltimoValor', () => {
  it('sigue a la fuente y, cuando esta se queda sin valor, conserva el último', () => {
    const fuente = signal<number | undefined>(undefined);
    const salida = TestBed.runInInjectionContext(() => conUltimoValor(() => fuente()));

    expect(salida()).toBeUndefined();

    fuente.set(1);
    expect(salida()).toBe(1);

    fuente.set(undefined);
    expect(salida()).toBe(1);

    fuente.set(2);
    expect(salida()).toBe(2);
  });
});

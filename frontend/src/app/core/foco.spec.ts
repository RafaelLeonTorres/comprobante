import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { enfocarArriba } from './foco';

describe('enfocarArriba', () => {
  const original = HTMLElement.prototype.scrollIntoView;
  let seccion: HTMLElement;
  let titulo: HTMLHeadingElement;

  beforeEach(() => {
    HTMLElement.prototype.scrollIntoView = vi.fn();
    seccion = document.createElement('section');
    titulo = document.createElement('h2');
    titulo.tabIndex = -1;
    seccion.append(titulo);
    document.body.append(seccion);
  });

  afterEach(() => {
    HTMLElement.prototype.scrollIntoView = original;
    seccion.remove();
  });

  function elementoDesplazado(): unknown {
    return vi.mocked(HTMLElement.prototype.scrollIntoView).mock.contexts[0];
  }

  it('enfoca el título sin que el foco desplace por su cuenta', () => {
    const enfocar = vi.spyOn(titulo, 'focus');

    enfocarArriba(titulo);

    expect(document.activeElement).toBe(titulo);
    expect(enfocar).toHaveBeenCalledWith({ preventScroll: true });
  });

  it('lleva arriba el bloque completo en el que está el título', () => {
    enfocarArriba(titulo);

    expect(HTMLElement.prototype.scrollIntoView).toHaveBeenCalledExactlyOnceWith({ block: 'start' });
    expect(elementoDesplazado()).toBe(seccion);
  });

  it('si el título no está dentro de un bloque, lleva arriba el título', () => {
    const suelto = document.createElement('h2');
    suelto.tabIndex = -1;
    document.body.append(suelto);

    enfocarArriba(suelto);
    suelto.remove();

    expect(elementoDesplazado()).toBe(suelto);
  });

  it('no hace nada si todavía no hay elemento', () => {
    expect(() => enfocarArriba(undefined)).not.toThrow();
    expect(() => enfocarArriba(null)).not.toThrow();
    expect(HTMLElement.prototype.scrollIntoView).not.toHaveBeenCalled();
  });
});

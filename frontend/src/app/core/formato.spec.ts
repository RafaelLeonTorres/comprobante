import { digitos, importe, nombreDeImpuesto, porcentaje } from './formato';

describe('formato', () => {
  it('un importe lleva los decimales de la moneda y separador de miles', () => {
    expect(importe(1160, 2)).toBe('1,160.00');
    expect(importe(1234567.891, 2)).toBe('1,234,567.89');
    expect(importe(5, 0)).toBe('5');
    expect(importe(0.5, 4)).toBe('0.5000');
  });

  it('un importe ausente no se muestra', () => {
    expect(importe(null)).toBe('');
    expect(importe(undefined)).toBe('');
  });

  it('una tasa se muestra como porcentaje sin ceros de más', () => {
    expect(porcentaje(0.16)).toBe('16 %');
    expect(porcentaje(0)).toBe('0 %');
    expect(porcentaje(0.106667)).toBe('10.6667 %');
    expect(porcentaje(0.04)).toBe('4 %');
  });

  it('el formato de dígitos de Angular fija los decimales', () => {
    expect(digitos(2)).toBe('1.2-2');
    expect(digitos(0)).toBe('1.0-0');
  });

  it('los impuestos se nombran por su clave del SAT', () => {
    expect(nombreDeImpuesto('001')).toBe('ISR');
    expect(nombreDeImpuesto('002')).toBe('IVA');
    expect(nombreDeImpuesto('003')).toBe('IEPS');
    expect(nombreDeImpuesto('999')).toBe('999');
  });
});

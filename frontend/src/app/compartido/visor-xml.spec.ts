import { TestBed } from '@angular/core/testing';

import { VisorXml } from './visor-xml';

const XML = '<?xml version="1.0" encoding="utf-8"?>\n<cfdi:Comprobante Version="4.0" Total="1160.00">\n  <cfdi:Emisor Rfc="EKU9003173C9"/>\n</cfdi:Comprobante>';

describe('VisorXml', () => {
  function pintar(xml: string, marcadas: readonly number[] = []) {
    TestBed.configureTestingModule({ imports: [VisorXml] });
    const fixture = TestBed.createComponent(VisorXml);
    fixture.componentRef.setInput('xml', xml);
    fixture.componentRef.setInput('lineasConHallazgos', marcadas);
    fixture.detectChanges();
    return { fixture, elemento: fixture.nativeElement as HTMLElement };
  }

  it('pinta una fila por línea con el texto del XML', () => {
    const { elemento } = pintar(XML);

    const filas = [...elemento.querySelectorAll<HTMLElement>('.linea')];
    expect(filas).toHaveLength(4);
    expect(filas.map((f) => f.textContent)).toEqual(XML.split('\n'));
    expect(filas[1]!.dataset['linea']).toBe('2');
  });

  it('resalta etiquetas, atributos y valores con clases distintas', () => {
    const { elemento } = pintar(XML);
    const fila = elemento.querySelector<HTMLElement>('[data-linea="2"]')!;

    expect(fila.querySelector('.etiqueta')!.textContent).toBe('cfdi:Comprobante');
    expect([...fila.querySelectorAll('.atributo')].map((e) => e.textContent)).toEqual(['Version', 'Total']);
    expect([...fila.querySelectorAll('.valor')].map((e) => e.textContent)).toEqual(['"4.0"', '"1160.00"']);
  });

  it('marca las líneas con hallazgos', () => {
    const { elemento } = pintar(XML, [2, 3]);

    const marcadas = [...elemento.querySelectorAll<HTMLElement>('.linea.con-hallazgo')].map((f) => f.dataset['linea']);
    expect(marcadas).toEqual(['2', '3']);
  });

  it('la región se alcanza con el teclado y tiene nombre para los lectores de pantalla', () => {
    const { fixture, elemento } = pintar(XML);
    fixture.componentRef.setInput('etiqueta', 'XML generado');
    fixture.detectChanges();

    const region = elemento.querySelector('[role="region"]')!;
    expect(region.getAttribute('tabindex')).toBe('0');
    expect(region.getAttribute('aria-label')).toBe('XML generado');
  });

  it('enfocarLinea resalta la línea, la lleva a la vista y la anuncia', () => {
    const alVista = vi.fn();
    HTMLElement.prototype.scrollIntoView = alVista;
    const { fixture, elemento } = pintar(XML);

    fixture.componentInstance.enfocarLinea(3);
    fixture.detectChanges();

    expect(elemento.querySelector('.linea.activa')!.getAttribute('data-linea')).toBe('3');
    expect(alVista).toHaveBeenCalledOnce();
    expect(elemento.querySelector('[role="status"]')!.textContent).toBe('Línea 3: <cfdi:Emisor Rfc="EKU9003173C9"/>');
  });

  it('una línea que no existe no hace nada', () => {
    const alVista = vi.fn();
    HTMLElement.prototype.scrollIntoView = alVista;
    const { fixture, elemento } = pintar(XML);

    fixture.componentInstance.enfocarLinea(99);
    fixture.detectChanges();

    expect(elemento.querySelector('.linea.activa')).toBeNull();
    expect(alVista).not.toHaveBeenCalled();
  });
});

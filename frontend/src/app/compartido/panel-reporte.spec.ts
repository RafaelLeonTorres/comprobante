import { TestBed } from '@angular/core/testing';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

import { registrarIconos } from '../core/iconos';
import { provideIdioma } from '../core/idioma';
import { Hallazgo, ReporteValidacion } from '../core/modelos';
import { PanelReporte } from './panel-reporte';

const certificado = {
  noCertificado: '30001000000500003416',
  rfc: 'EKU9003173C9',
  nombre: 'ESCUELA KEMPER URGATE',
  validoDesde: '2023-05-18T11:43:51+00:00',
  validoHasta: '2027-05-18T11:43:51+00:00',
};

function reporte(parcial: Partial<ReporteValidacion> = {}): ReporteValidacion {
  return { esValido: true, hallazgos: [], cadenaOriginal: '||4.0|A|1||', selloValido: true, certificado, ...parcial };
}

const total: Hallazgo = {
  codigo: 'CFDI40119',
  severidad: 'error',
  nodo: 'cfdi:Comprobante',
  atributo: 'Total',
  mensaje: 'El Total (1161.00) no corresponde con lo calculado.',
  encontrado: '1161.00',
  esperado: '1160.00',
  linea: 2,
};

describe('PanelReporte', () => {
  function pintar(datos: ReporteValidacion) {
    TestBed.configureTestingModule({ imports: [PanelReporte], providers: [provideIdioma()] });
    registrarIconos(TestBed.inject(MatIconRegistry), TestBed.inject(DomSanitizer));
    const fixture = TestBed.createComponent(PanelReporte);
    fixture.componentRef.setInput('reporte', datos);
    fixture.detectChanges();
    return { fixture, elemento: fixture.nativeElement as HTMLElement };
  }

  it('sin hallazgos y con el sello bueno lo celebra y muestra el certificado', () => {
    const { elemento } = pintar(reporte());

    const veredicto = elemento.querySelector('[role="status"]')!;
    expect(veredicto.textContent).toContain('Sin hallazgos');
    expect(veredicto.textContent).toContain('El sello corresponde a la cadena original');
    expect(veredicto.classList).toContain('correcto');
    expect(elemento.querySelector('.certificado')!.textContent).toContain('30001000000500003416');
    expect(elemento.querySelector('.certificado')!.textContent).toContain('18 may 2027');
    expect(elemento.querySelector('.grupo')).toBeNull();
  });

  it('cuenta los errores y agrupa los hallazgos por nodo con su código del SAT', () => {
    const uso: Hallazgo = {
      codigo: 'CFDI40160',
      severidad: 'error',
      nodo: 'cfdi:Comprobante/cfdi:Receptor',
      atributo: 'UsoCFDI',
      mensaje: 'La clave Z99 no está en el catálogo c_UsoCFDI del SAT.',
      encontrado: 'Z99',
      linea: 4,
    };
    const sello: Hallazgo = { ...total, codigo: 'CFDI40102', atributo: 'Sello', mensaje: 'El sello no corresponde.', encontrado: null, esperado: null };
    const { elemento } = pintar(reporte({ esValido: false, selloValido: false, hallazgos: [total, uso, sello] }));

    expect(elemento.querySelector('[role="status"]')!.textContent).toContain('El XML tiene 3 errores');
    expect(elemento.querySelector('[role="status"]')!.textContent).toContain('El sello no corresponde');
    const grupos = [...elemento.querySelectorAll('.grupo')];
    expect(grupos).toHaveLength(2);
    expect(grupos[0]!.querySelectorAll('.hallazgo')).toHaveLength(2);
    expect(grupos[0]!.querySelector('h3')!.textContent).toContain('cfdi:Comprobante');
    expect(grupos[1]!.querySelector('h3 code')!.textContent).toBe('cfdi:Receptor');
    expect(grupos[1]!.querySelector('.ruta')!.textContent).toContain('cfdi:Comprobante');
    expect(grupos[1]!.querySelector('.codigo')!.textContent).toBe('CFDI40160');
    expect(grupos[1]!.querySelector('.meta')!.textContent).toContain('UsoCFDI');
    expect(grupos[1]!.querySelector('.meta')!.textContent).toContain('Z99');
  });

  it('muestra lo encontrado y lo esperado', () => {
    const { elemento } = pintar(reporte({ esValido: false, hallazgos: [total] }));

    const meta = elemento.querySelector('.meta')!.textContent!;
    expect(meta).toContain('Encontrado 1161.00');
    expect(meta).toContain('Esperado 1160.00');
  });

  it('el botón de la línea avisa cuál se quiere ver', () => {
    const { fixture, elemento } = pintar(reporte({ esValido: false, hallazgos: [total] }));
    const vistas: number[] = [];
    fixture.componentInstance.irALinea.subscribe((linea) => vistas.push(linea));

    elemento.querySelector<HTMLButtonElement>('button.linea')!.click();

    expect(elemento.querySelector('button.linea')!.textContent).toContain('Línea 2');
    expect(vistas).toEqual([2]);
  });

  it('solo con advertencias no es un error, pero tampoco está limpio', () => {
    const aviso: Hallazgo = { ...total, codigo: 'CMP004', severidad: 'advertencia', mensaje: 'Complemento sin revisar.', encontrado: null, esperado: null };
    const { elemento } = pintar(reporte({ esValido: true, hallazgos: [aviso] }));

    const veredicto = elemento.querySelector('[role="status"]')!;
    expect(veredicto.textContent).toContain('Sin errores, con 1 advertencia');
    expect(veredicto.classList).not.toContain('correcto');
    expect(veredicto.classList).not.toContain('con-error');
    expect(elemento.querySelector('.hallazgo')!.classList).toContain('advertencia');
    expect(elemento.querySelector('.hallazgo .solo-lectores')!.textContent).toBe('Advertencia');
  });

  it('cuando no se pudo comprobar el sello lo dice', () => {
    const { elemento } = pintar(reporte({ selloValido: null, certificado: null }));

    expect(elemento.querySelector('[role="status"]')!.textContent).toContain('No se pudo comprobar el sello');
    expect(elemento.querySelector('.certificado')).toBeNull();
  });

  it('la cadena original va en un bloque plegable con su botón de copiar', () => {
    const { elemento } = pintar(reporte({ cadenaOriginal: '||4.0|A|1|2026-11-21T09:30:00||' }));

    const cadena = elemento.querySelector('details.cadena')!;
    expect(cadena.querySelector('summary')!.textContent).toBe('Cadena original');
    expect(cadena.querySelector('pre')!.textContent).toBe('||4.0|A|1|2026-11-21T09:30:00||');
    expect(cadena.querySelector('app-boton-copiar')).not.toBeNull();
  });

  it('sin cadena original no hay bloque', () => {
    const { elemento } = pintar(reporte({ cadenaOriginal: null }));

    expect(elemento.querySelector('details.cadena')).toBeNull();
  });
});

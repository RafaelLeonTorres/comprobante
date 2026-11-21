import { LIMITE_DE_RESALTADO, resaltarXml, tokenizar } from './resaltado-xml';

const tipos = (xml: string) => tokenizar(xml).map((t) => `${t.tipo}:${t.texto}`);

describe('resaltado de XML', () => {
  it('separa etiqueta, atributos y valores de un elemento vacío', () => {
    expect(tipos('<cfdi:Comprobante Version="4.0" Total="1160.00"/>')).toEqual([
      'puntuacion:<',
      'etiqueta:cfdi:Comprobante',
      'texto: ',
      'atributo:Version',
      'puntuacion:=',
      'valor:"4.0"',
      'texto: ',
      'atributo:Total',
      'puntuacion:=',
      'valor:"1160.00"',
      'puntuacion:/>',
    ]);
  });

  it('reconoce la apertura, el texto y el cierre', () => {
    expect(tipos('<a>hola</a>')).toEqual([
      'puntuacion:<',
      'etiqueta:a',
      'puntuacion:>',
      'texto:hola',
      'puntuacion:</',
      'etiqueta:a',
      'puntuacion:>',
    ]);
  });

  it('la declaración, los comentarios y el CDATA son un solo token', () => {
    expect(tipos('<?xml version="1.0" encoding="utf-8"?><!-- nota <b> --><a><![CDATA[<x>&]]></a>')).toEqual([
      'declaracion:<?xml version="1.0" encoding="utf-8"?>',
      'comentario:<!-- nota <b> -->',
      'puntuacion:<',
      'etiqueta:a',
      'puntuacion:>',
      'texto:<![CDATA[<x>&]]>',
      'puntuacion:</',
      'etiqueta:a',
      'puntuacion:>',
    ]);
  });

  it('un valor con comillas simples o con ">" dentro se mantiene entero', () => {
    const t = tokenizar(`<a x='1 > 0' y="a'b"/>`);

    expect(t.filter((x) => x.tipo === 'valor').map((x) => x.texto)).toEqual([`'1 > 0'`, `"a'b"`]);
  });

  it('reparte en líneas, también cuando una etiqueta se parte en varias', () => {
    const lineas = resaltarXml('<?xml version="1.0"?>\r\n<a\r\n  x="1"\r\n  y="2">\r\n</a>');

    expect(lineas).toHaveLength(5);
    expect(lineas[0]![0]!.tipo).toBe('declaracion');
    expect(lineas[2]!.map((t) => t.texto).join('')).toBe('  x="1"');
    expect(lineas[2]!.find((t) => t.tipo === 'atributo')!.texto).toBe('x');
    expect(lineas[3]!.find((t) => t.tipo === 'valor')!.texto).toBe('"2"');
  });

  it('un salto de línea suelto (\\r) cuenta igual que \\n, como lo cuenta el validador', () => {
    expect(resaltarXml('<a>\r<b/>\n<c/>\r\n</a>')).toHaveLength(4);
  });

  it('una línea en blanco queda como una línea vacía', () => {
    const lineas = resaltarXml('<a>\n\n</a>');

    expect(lineas).toHaveLength(3);
    expect(lineas[1]).toEqual([]);
  });

  it.each([
    '',
    'texto sin etiquetas',
    '<a',
    '<a b',
    '<a b=',
    '<a b="sin cerrar',
    '<a b=1 c=\'2\'>x</a>',
    '<!-- sin cerrar',
    '<![CDATA[ sin cerrar',
    '<?xml sin cerrar',
    '<a = = "x"/>',
    '</>',
    '<>',
    '<a/ b>',
    '<a>&nbsp;&amp;</a><b/>',
  ])('con XML roto o raro (%j) no falla y no pierde ni inventa texto', (xml) => {
    const unido = resaltarXml(xml)
      .map((linea) => linea.map((t) => t.texto).join(''))
      .join('\n');

    expect(unido).toBe(xml.replace(/\r\n|\r|\n/g, '\n'));
  });

  it('un XML muy grande va sin resaltar, línea por línea', () => {
    const grande = '<a x="1"/>\n'.repeat(Math.ceil(LIMITE_DE_RESALTADO / 10) + 1);

    const lineas = resaltarXml(grande);

    expect(lineas[0]).toEqual([{ tipo: 'texto', texto: '<a x="1"/>' }]);
    expect(lineas.at(-1)).toEqual([]);
  });
});

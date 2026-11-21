/**
 * Resaltado de sintaxis de XML hecho a mano: un recorrido de una sola pasada que separa etiquetas, atributos, valores,
 * comentarios y texto. No valida nada ni falla con XML roto: lo que no entiende lo deja como texto, y lo que sale de aquí,
 * unido de nuevo, es siempre el mismo texto de entrada (sin los saltos de línea, que son los separadores de las líneas).
 */
export type TipoToken = 'puntuacion' | 'etiqueta' | 'atributo' | 'valor' | 'comentario' | 'declaracion' | 'texto';

export interface Token {
  readonly tipo: TipoToken;
  readonly texto: string;
}

/** Pasado este tamaño no se resalta: miles de nodos del DOM por un XML enorme pesan más de lo que ayudan. */
export const LIMITE_DE_RESALTADO = 150_000;

const SALTO_DE_LINEA = /\r\n|\r|\n/;

function esEspacio(c: string | undefined): boolean {
  return c === ' ' || c === '\n' || c === '\r' || c === '\t';
}

/** El XML como una lista plana de tokens. */
export function tokenizar(xml: string): Token[] {
  const tokens: Token[] = [];
  const n = xml.length;
  const poner = (tipo: TipoToken, texto: string): void => {
    if (texto) {
      tokens.push({ tipo, texto });
    }
  };

  /** Desde <c>desde</c> hasta el cierre <c>fin</c> (incluido), o hasta el final si no cierra. */
  const hasta = (desde: number, fin: string, saltar: number): number => {
    const posicion = xml.indexOf(fin, desde + saltar);
    return posicion === -1 ? n : posicion + fin.length;
  };

  let i = 0;
  while (i < n) {
    if (xml[i] !== '<') {
      const siguiente = xml.indexOf('<', i);
      const j = siguiente === -1 ? n : siguiente;
      poner('texto', xml.slice(i, j));
      i = j;
    } else if (xml.startsWith('<!--', i)) {
      const j = hasta(i, '-->', 4);
      poner('comentario', xml.slice(i, j));
      i = j;
    } else if (xml.startsWith('<![CDATA[', i)) {
      const j = hasta(i, ']]>', 9);
      poner('texto', xml.slice(i, j));
      i = j;
    } else if (xml.startsWith('<?', i)) {
      const j = hasta(i, '?>', 2);
      poner('declaracion', xml.slice(i, j));
      i = j;
    } else if (xml.startsWith('<!', i)) {
      const j = hasta(i, '>', 2);
      poner('declaracion', xml.slice(i, j));
      i = j;
    } else {
      i = etiqueta(xml, i, poner);
    }
  }

  return tokens;
}

/** Una etiqueta de apertura o de cierre con sus atributos; devuelve dónde termina. */
function etiqueta(xml: string, inicio: number, poner: (tipo: TipoToken, texto: string) => void): number {
  const n = xml.length;
  let j = inicio + 1;
  if (xml[j] === '/') {
    j++;
  }

  poner('puntuacion', xml.slice(inicio, j));

  let k = j;
  while (k < n && !esEspacio(xml[k]) && xml[k] !== '/' && xml[k] !== '>') {
    k++;
  }

  poner('etiqueta', xml.slice(j, k));
  j = k;

  while (j < n) {
    k = j;
    while (k < n && esEspacio(xml[k])) {
      k++;
    }

    poner('texto', xml.slice(j, k));
    j = k;
    if (j >= n) {
      break;
    }

    if (xml[j] === '>') {
      poner('puntuacion', '>');
      return j + 1;
    }

    if (xml[j] === '/') {
      if (xml[j + 1] === '>') {
        poner('puntuacion', '/>');
        return j + 2;
      }

      poner('puntuacion', '/');
      j++;
      continue;
    }

    k = j;
    while (k < n && !esEspacio(xml[k]) && xml[k] !== '=' && xml[k] !== '/' && xml[k] !== '>') {
      k++;
    }

    if (k === j) {
      // Un carácter suelto (por ejemplo un "=" sin atributo): se deja como texto para avanzar siempre.
      poner('texto', xml[j]!);
      j++;
      continue;
    }

    poner('atributo', xml.slice(j, k));
    j = k;

    k = j;
    while (k < n && esEspacio(xml[k])) {
      k++;
    }

    if (xml[k] !== '=') {
      continue;
    }

    poner('texto', xml.slice(j, k));
    poner('puntuacion', '=');
    j = k + 1;
    k = j;
    while (k < n && esEspacio(xml[k])) {
      k++;
    }

    poner('texto', xml.slice(j, k));
    j = k;
    const comilla = xml[j];
    if (comilla === '"' || comilla === "'") {
      const cierre = xml.indexOf(comilla, j + 1);
      k = cierre === -1 ? n : cierre + 1;
    } else {
      k = j;
      while (k < n && !esEspacio(xml[k]) && xml[k] !== '/' && xml[k] !== '>') {
        k++;
      }
    }

    poner('valor', xml.slice(j, k));
    j = k;
  }

  return n;
}

/** Reparte los tokens en líneas, cortando los que traen saltos de línea. La primera línea es la 1. */
export function enLineas(tokens: readonly Token[]): Token[][] {
  const lineas: Token[][] = [[]];
  for (const token of tokens) {
    token.texto.split(SALTO_DE_LINEA).forEach((parte, indice) => {
      if (indice > 0) {
        lineas.push([]);
      }

      if (parte) {
        lineas[lineas.length - 1]!.push({ tipo: token.tipo, texto: parte });
      }
    });
  }

  return lineas;
}

/** El XML listo para pintar: una lista de líneas, cada una con sus tokens. Los XML muy grandes van sin resaltar. */
export function resaltarXml(xml: string): Token[][] {
  if (xml.length > LIMITE_DE_RESALTADO) {
    return xml.split(SALTO_DE_LINEA).map((texto) => (texto ? [{ tipo: 'texto' as const, texto }] : []));
  }

  return enLineas(tokenizar(xml));
}

// Toma las capturas del README con Edge o Chrome sin interfaz, hablándole por el protocolo DevTools.
// No instala nada: usa el WebSocket que trae Node 22 o más reciente. La app tiene que estar corriendo
// (docker compose up -d --wait) y las capturas salen siempre igual porque usan los ejemplos que ya trae.
//
//   node docs/capturas/tomar-capturas.mjs [url]      por omisión, http://localhost:8081
//
// Si no encuentra el navegador, dile cuál con la variable NAVEGADOR (la ruta del ejecutable).
import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const base = (process.argv[2] ?? 'http://localhost:8081').replace(/\/$/, '');
const carpeta = dirname(fileURLToPath(import.meta.url));
const ANCHO = 1200; // píxeles CSS: el ancho de un escritorio
const ESCALA = 2; // píxeles reales por píxel CSS: se ven nítidas también en pantallas de alta densidad
const MARGEN = 16; // lo que se deja por encima de una sección al recortarla, para que no asome lo de arriba

const pausa = (ms) => new Promise((resolver) => setTimeout(resolver, ms));

function buscarNavegador() {
  const archivos = process.env['ProgramFiles(x86)'];
  const programas = process.env.ProgramFiles;
  const candidatos = [
    process.env.NAVEGADOR,
    archivos && join(archivos, 'Microsoft/Edge/Application/msedge.exe'),
    programas && join(programas, 'Microsoft/Edge/Application/msedge.exe'),
    archivos && join(archivos, 'Google/Chrome/Application/chrome.exe'),
    programas && join(programas, 'Google/Chrome/Application/chrome.exe'),
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
    '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge',
    '/usr/bin/google-chrome',
    '/usr/bin/chromium',
    '/usr/bin/chromium-browser',
    '/usr/bin/microsoft-edge',
  ];
  return candidatos.find((ruta) => ruta && existsSync(ruta));
}

const navegador = buscarNavegador();
if (!navegador) {
  console.error('No encontré Edge ni Chrome. Pon la ruta del ejecutable en la variable NAVEGADOR.');
  process.exit(1);
}

const perfil = mkdtempSync(join(tmpdir(), 'comprobante-capturas-'));
const proceso = spawn(
  navegador,
  [
    '--headless=new',
    '--remote-debugging-port=0',
    `--user-data-dir=${perfil}`,
    '--no-first-run',
    '--no-default-browser-check',
    '--hide-scrollbars',
    '--disable-extensions',
    'about:blank',
  ],
  { stdio: 'ignore' },
);

let ws;
try {
  // Con el puerto 0 el navegador elige uno libre y lo anota en este archivo.
  const archivoPuerto = join(perfil, 'DevToolsActivePort');
  let direccion;
  for (let i = 0; i < 200 && !direccion; i++) {
    if (existsSync(archivoPuerto)) {
      const [puerto, ruta] = readFileSync(archivoPuerto, 'utf8').trim().split(/\r?\n/);
      if (puerto && ruta) direccion = `ws://127.0.0.1:${puerto}${ruta}`;
    }
    if (!direccion) await pausa(100);
  }
  if (!direccion) throw new Error('El navegador no abrió el puerto de depuración.');

  ws = new WebSocket(direccion);
  await new Promise((resolver, rechazar) => {
    ws.addEventListener('open', resolver, { once: true });
    ws.addEventListener('error', () => rechazar(new Error('No pude conectar con el navegador.')), { once: true });
  });

  let contador = 0;
  const pendientes = new Map();
  const oyentes = new Set();
  ws.addEventListener('message', (evento) => {
    const mensaje = JSON.parse(evento.data);
    if (mensaje.id) {
      const espera = pendientes.get(mensaje.id);
      pendientes.delete(mensaje.id);
      if (mensaje.error) espera?.rechazar(new Error(`${espera.metodo}: ${mensaje.error.message}`));
      else espera?.resolver(mensaje.result);
    } else {
      for (const oyente of oyentes) oyente(mensaje);
    }
  });

  let sesion;
  const enviar = (metodo, params = {}, conSesion = true) =>
    new Promise((resolver, rechazar) => {
      const id = ++contador;
      pendientes.set(id, { resolver, rechazar, metodo });
      ws.send(JSON.stringify({ id, method: metodo, params, ...(conSesion && sesion ? { sessionId: sesion } : {}) }));
    });
  const esperarEvento = (metodo) =>
    new Promise((resolver) => {
      const oyente = (mensaje) => {
        if (mensaje.method === metodo && mensaje.sessionId === sesion) {
          oyentes.delete(oyente);
          resolver(mensaje.params);
        }
      };
      oyentes.add(oyente);
    });

  const { targetId } = await enviar('Target.createTarget', { url: 'about:blank' }, false);
  ({ sessionId: sesion } = await enviar('Target.attachToTarget', { targetId, flatten: true }, false));
  await enviar('Page.enable');
  await enviar('Runtime.enable');

  const evaluar = async (expresion) => {
    const { result, exceptionDetails } = await enviar('Runtime.evaluate', {
      expression: expresion,
      awaitPromise: true,
      returnByValue: true,
    });
    if (exceptionDetails) throw new Error(exceptionDetails.exception?.description ?? exceptionDetails.text);
    return result.value;
  };
  const esperar = async (expresion, descripcion, limiteMs = 20000) => {
    const limite = Date.now() + limiteMs;
    while (Date.now() < limite) {
      if (await evaluar(`Boolean(${expresion})`)) return;
      await pausa(100);
    }
    throw new Error(`Se acabó el tiempo esperando: ${descripcion}`);
  };

  const ponerVentana = (alto) =>
    enviar('Emulation.setDeviceMetricsOverride', { width: ANCHO, height: Math.round(alto), deviceScaleFactor: ESCALA, mobile: false });

  /** Abre una ruta con el tema indicado y espera a que la página y sus tipografías estén listas. */
  const abrir = async (ruta, tema, alto = 900) => {
    await enviar('Emulation.setEmulatedMedia', {
      features: [
        { name: 'prefers-color-scheme', value: tema },
        { name: 'prefers-reduced-motion', value: 'reduce' },
      ],
    });
    await ponerVentana(alto);
    const cargada = esperarEvento('Page.loadEventFired');
    const { errorText } = await enviar('Page.navigate', { url: base + ruta });
    if (errorText) throw new Error(`No pude abrir ${base + ruta} (${errorText}). ¿Está corriendo la app? docker compose up -d --wait`);
    await cargada;
    await esperar("document.querySelector('main h1')", 'el título de la página');
    await evaluar('document.fonts.ready.then(() => true)');
  };

  const clic = (texto) =>
    evaluar(`(() => {
      const boton = [...document.querySelectorAll('main button')].find((b) => b.textContent.includes(${JSON.stringify(texto)}));
      if (!boton) throw new Error('No encontré el botón: ' + ${JSON.stringify(texto)});
      boton.click();
      return true;
    })()`);

  const abrirDetalle = (resumen) =>
    evaluar(`(() => {
      const detalle = [...document.querySelectorAll('main details')].find((d) => d.querySelector('summary')?.textContent.includes(${JSON.stringify(resumen)}));
      if (!detalle) throw new Error('No encontré el detalle: ' + ${JSON.stringify(resumen)});
      detalle.open = true;
      return true;
    })()`);

  const irA = (alturaY) => evaluar(`window.scrollTo({ top: ${Math.round(alturaY)}, behavior: 'instant' })`);
  const posicionDe = (selector, texto = '') =>
    evaluar(`(() => {
      const elemento = [...document.querySelectorAll(${JSON.stringify(selector)})].find((e) => e.textContent.includes(${JSON.stringify(texto)}));
      if (!elemento) throw new Error('No encontré ' + ${JSON.stringify(selector + ' ' + texto)});
      const caja = elemento.getBoundingClientRect();
      return { arriba: caja.top + window.scrollY, alto: caja.height };
    })()`);

  const seccionDe = (titulo) =>
    evaluar(`(() => {
      const h2 = [...document.querySelectorAll('main h2')].find((h) => h.textContent.trim() === ${JSON.stringify(titulo)});
      if (!h2) throw new Error('No encontré la sección ' + ${JSON.stringify(titulo)});
      const caja = (h2.closest('section') ?? h2).getBoundingClientRect();
      return { arriba: caja.top + window.scrollY, alto: caja.height };
    })()`);

  const resultados = [];
  const guardar = async (archivo) => {
    await evaluar('document.activeElement?.blur?.()');
    await pausa(500);
    const { data } = await enviar('Page.captureScreenshot', { format: 'png', fromSurface: true });
    const bytes = Buffer.from(data, 'base64');
    writeFileSync(join(carpeta, archivo), bytes);
    resultados.push({ archivo, ancho: bytes.readUInt32BE(16), alto: bytes.readUInt32BE(20), kb: Math.round(bytes.length / 1024) });
  };
  const alturaDeLaPagina = () => evaluar('Math.ceil(document.documentElement.scrollHeight)');

  // --- Inicio, claro y oscuro: la página entera ---
  for (const [archivo, tema] of [
    ['inicio.png', 'light'],
    ['inicio-oscuro.png', 'dark'],
  ]) {
    await abrir('/', tema);
    await esperar("document.body.innerText.includes('Catálogos del SAT publicados')", 'el estado de los catálogos');
    await ponerVentana(await alturaDeLaPagina());
    await guardar(archivo);
  }

  // --- Generar con un ejemplo: el formulario y los totales en vivo ---
  await abrir('/generar', 'light');
  await clic('Con retenciones y descuento');
  await esperar(
    "document.querySelector('section.totales')?.innerText.includes('MXN') && !document.querySelector('section.totales').innerText.includes('Agrega un concepto')",
    'los totales del ejemplo',
  );
  const emisor = await seccionDe('Emisor');
  await ponerVentana(emisor.arriba + emisor.alto + 24);
  await irA(0);
  await guardar('generar.png');

  // --- Resultado de generar: el XML sellado, la cadena original y el sello ---
  await clic('Generar CFDI');
  await esperar("document.querySelector('section.resultado app-revision-xml')?.innerText.includes('<cfdi:Comprobante')", 'el XML generado');
  await abrirDetalle('Cadena original');
  const resultado = await posicionDe('section.resultado');
  await ponerVentana(MARGEN + resultado.alto + 24);
  await irA(resultado.arriba - MARGEN);
  await guardar('resultado.png');

  // --- Validar un XML con errores: el reporte de hallazgos ---
  await abrir('/validar', 'light');
  await clic('Un CFDI con errores');
  await esperar("document.body.innerText.includes('Resultado: Un CFDI con errores')", 'el reporte de la validación');
  const reporte = await posicionDe('section.resultado');
  await ponerVentana(Math.min(MARGEN + reporte.alto + 24, 1600));
  await irA(reporte.arriba - MARGEN);
  await guardar('validar.png');

  console.table(resultados);
  await enviar('Browser.close', {}, false).catch(() => {});
} finally {
  try {
    ws?.close();
  } catch {
    // ya estaba cerrado
  }
  proceso.kill();
  await pausa(500);
  rmSync(perfil, { recursive: true, force: true, maxRetries: 5, retryDelay: 300 });
}

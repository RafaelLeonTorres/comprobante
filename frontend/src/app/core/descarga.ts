/** Descarga un archivo que ya está en el navegador, sin pasar por el servidor. */
export function descargarArchivo(nombre: string, archivo: Blob): void {
  const url = URL.createObjectURL(archivo);
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = nombre;
  enlace.rel = 'noopener';
  document.body.append(enlace);
  enlace.click();
  enlace.remove();
  // Dar tiempo a que el navegador empiece la descarga antes de soltar el objeto.
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}

/** Descarga un texto como archivo: el XML ya está en el navegador. */
export function descargarTexto(nombre: string, contenido: string, tipo = 'application/xml'): void {
  descargarArchivo(nombre, new Blob([contenido], { type: `${tipo};charset=utf-8` }));
}

/**
 * El nombre de archivo que propone el servidor en <c>Content-Disposition</c>, con o sin comillas y en su forma extendida
 * (<c>filename*=UTF-8''...</c>); si no trae ninguno, <c>porOmision</c>.
 */
export function nombreDeDescarga(cabecera: string | null, porOmision = 'comprobante.pdf'): string {
  if (!cabecera) {
    return porOmision;
  }

  const extendido = /filename\*\s*=\s*UTF-8''([^;]+)/i.exec(cabecera);
  if (extendido?.[1]) {
    try {
      return decodeURIComponent(extendido[1].trim());
    } catch {
      // Un porcentaje mal escrito: se intenta con la forma simple.
    }
  }

  const simple = /filename\s*=\s*"?([^";]+)"?/i.exec(cabecera);
  return simple?.[1]?.trim() || porOmision;
}

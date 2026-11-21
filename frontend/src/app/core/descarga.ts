/** Descarga un texto como archivo, sin pasar por el servidor: el XML ya está en el navegador. */
export function descargarTexto(nombre: string, contenido: string, tipo = 'application/xml'): void {
  const url = URL.createObjectURL(new Blob([contenido], { type: `${tipo};charset=utf-8` }));
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

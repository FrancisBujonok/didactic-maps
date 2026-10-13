// files.js - elegir archivos del disco y descargar archivos generados.

// Abre el selector de archivos. Devuelve { name, content } o null si se cancela.
// Si asText es true, content es el texto del archivo; si no, es un "data URL" (sirve para <img>).
export function pickFile(accept, asText) {
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = accept;

    input.onchange = () => {
      const file = input.files && input.files[0];
      if (!file) {
        resolve(null);
        return;
      }
      const reader = new FileReader();
      reader.onload = () => resolve({ name: file.name, content: String(reader.result) });
      reader.onerror = () => resolve(null);
      if (asText) reader.readAsText(file);
      else reader.readAsDataURL(file);
    };
    input.oncancel = () => resolve(null);

    input.click();
  });
}

// Descarga un texto como archivo.
export function download(filename, text) {
  const blob = new Blob([text], { type: 'application/json' });
  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();

  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

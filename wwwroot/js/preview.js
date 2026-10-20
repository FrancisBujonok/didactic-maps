// preview.js - convierte píxeles calculados en C# en una imagen (miniaturas de las plantillas).
//
// C# manda los píxeles como bytes RGBA (rojo, verde, azul, opacidad), fila por fila. Un canvas
// los dibuja tal cual y devuelve la imagen como data URL, lista para usar en un <img>.
// Se hace en JavaScript porque el navegador ya sabe armar un PNG; en C# haría falta una librería.

export function toDataUrl(width, height, rgba) {
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext('2d');
  context.putImageData(new ImageData(new Uint8ClampedArray(rgba), width, height), 0, 0);
  return canvas.toDataURL('image/png');
}

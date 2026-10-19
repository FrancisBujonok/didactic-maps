# Mapa de Mundos

Visor y editor de mapas para worldbuilding, hecho con Blazor WebAssembly (C#). Todo corre en el navegador: no hay servidor ni base de datos externa.

## Ejecutar

1. Instalá el SDK de .NET 10 y comprobá la versión con `dotnet --version`.
2. Desde esta carpeta ejecutá `dotnet run`.
3. Abrí `http://localhost:5000` en el navegador.

## Probar

1. "Cargar imagen" y elegí un PNG o SVG (por ejemplo, uno exportado desde Azgaar).
2. Activá "Colocar pines" y hacé clic en el mapa.
3. Completá la ficha del pin en el panel derecho.
4. Creá otra capa, cambiale el color y ocultala con su casilla.
5. Recargá la página: todo sigue ahí (se guarda en IndexedDB).
6. "Exportar" genera un `.mundo.json` con todo, imagen incluida. "Importar" lo vuelve a cargar.
7. En "Generar mapa", "Todo al azar" crea un continente nuevo. Cambiá una sola semilla y volvé a "Generar" para ver qué controla cada etapa. El estilo "Celdas" muestra la grilla del generador.
8. Con un mapa generado, "Ver celdas" en la barra: hacé clic en el mapa para ver terreno, altura, temperatura, humedad y ríos de esa celda.
9. Shift + clic agrega o quita celdas y Shift + arrastrar las pinta. En el panel derecho podés cambiarles el terreno, subirlas, bajarlas, hacerlas tierra o agua, o restaurarlas. Los cambios se guardan y se mantienen al volver a generar con la misma semilla de continente.
10. En "Reinos" (panel izquierdo) creá un reino; con celdas elegidas, asignalas desde el panel derecho. En la barra, "Vista" muestra el mapa por reinos, altura, temperatura o humedad, con su leyenda.
11. Cada pin tiene un tipo con su ícono (ciudad, aldea, puerto, posada, fortaleza, torre, templo, bosque sagrado, cueva, mazmorra, mina, ruina, sitio abandonado, campo de batalla y otros). Con "Colocar pines" activo, la barra de íconos de arriba del mapa elige el tipo de los pines nuevos; en la ficha, "Cambiar" abre la grilla de tipos. Si un pin está dentro de un reino, su ficha permite marcarlo como capital: se dibuja con una estrella y un anillo doble.

## Cómo está organizado

- `Models/`: los datos (`MapProject`, `MapLayer`, `MapPin`) y el formato del archivo exportado.
- `Services/ProjectService.cs`: guarda el proyecto abierto y se encarga de persistir, exportar e importar.
- `Components/MapEditor.razor`: la interfaz principal (paneles, capas, ficha, pines).
- `Components/GeneratorPanel.razor`: el panel "Generar mapa".
- `Components/CellInfo.razor` y `CellPanel.razor`: la ficha de las celdas seleccionadas y las herramientas para editarlas.
- `Components/RegionPanel.razor`: la lista de reinos.
- `Components/PinIcon.razor`: los íconos de los tipos de pin (SVG propios).
- `Components/KindPicker.razor`: el selector visual de tipo (barra de colocación y ficha).
- `Generation/`: el generador de mapas, una clase por etapa (malla, continente, clima, ríos, biomas), el dibujo en SVG y las vistas (`ThematicRenderer`).
- `wwwroot/js/mapview.js`: zoom, arrastre y clics sobre el mapa.
- `wwwroot/js/storage.js` y `files.js`: IndexedDB y selector/descarga de archivos.

Decisiones de diseño que conviene entender:

- Los pines guardan su posición **normalizada** (0 a 1), no en píxeles.
- El zoom y el arrastre viven en JavaScript para que sean fluidos; C# solo recibe los eventos importantes.
- La imagen se guarda aparte del proyecto porque pesa mucho más.
- El zoom máximo depende de la imagen: 2x para PNG y JPG (más allá se ven borrosos) y 16x para SVG, que es vectorial y no pierde nitidez. Para mapas de Azgaar conviene exportar en SVG.

## Ideas para modificar (ejercicios)

1. Agregá un tipo de pin nuevo en `MapPin.KindGroups` y dibujale su ícono en `PinIcon.razor`.
2. Cambiá la forma del pin en `app.css` (por ejemplo, un cuadrado o una gota).
3. Hacé que el tamaño del pin dependa de su tipo.
4. Agregá un campo nuevo a la ficha, como "Población" o "Facción".
5. Agregá un buscador que filtre los pines por título.
6. Hacé que al hacer doble clic en un pin el mapa se centre en él (hace falta una función nueva en `mapview.js`).

## Pendiente

- Importar los CSV/GeoJSON de Azgaar para crear pines automáticamente (primero hay que exportar uno y mirar sus columnas).
- Vincular cada pin con una ficha del mundo (wiki) en lugar de una descripción suelta.
- Generador: sombra de lluvia, nombres de lugares y generación sin congelar la interfaz.

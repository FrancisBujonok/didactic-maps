# Mapa de Mundos

Visor y editor de mapas para worldbuilding, hecho con Blazor WebAssembly (C#). Todo corre en el navegador: no hay servidor ni base de datos externa.

## Ejecutar

1. Instalá el SDK de .NET 10 y comprobá la versión con `dotnet --version`.
2. Desde esta carpeta ejecutá `dotnet run`.
3. Abrí `http://localhost:5000` en el navegador.

## Probar

1. "Cargar imagen" y elegí un PNG o SVG (por ejemplo, uno exportado desde Azgaar).
2. En el lomo (la columna oscura de la izquierda), activá "Colocar pines" y hacé clic en el mapa.
3. Completá la ficha del pin, que aparece como una hoja sobre el mapa. La "x" la cierra.
4. Creá otra capa, cambiale el color y ocultala con su casilla.
5. Recargá la página: todo sigue ahí (se guarda en IndexedDB).
6. "Exportar" genera un `.mundo.json` con todo, imagen incluida. "Importar" lo vuelve a cargar.
7. En "Generar mapa", "Relieve" elige la forma de la tierra entre 10 plantillas con miniaturas (continente, continentes, pangea, archipiélago, isla volcánica, península, istmo, mediterráneo, atolón, fragmentado) o "Dibujo": con "Dibujar en el mapa" trazás a mano islas, lagos o bahías, cordilleras y zonas de un bioma elegido (desierto, selva, taiga...), y "Fidelidad al trazo" decide cuánto se respeta la línea (100 %: la sigue; 10 %: inventa casi todo), con vista previa. Con las otras plantillas, "Retoques" + "Retocar en el mapa" usa las mismas herramientas encima del mapa generado: agregar islas, borrar tierra con "Agua", sumar cordilleras o biomas. La "Goma" borra un trazo con un clic y "Deshacer" revierte el último cambio. "Costas" elige el detalle del borde de la tierra (suaves, irregulares o muy recortadas, con líneas de agua como en los atlas antiguos). "Tierra" elige el detalle del interior: lisa, con sombreado de relieve, o atlas (sombreado más montañas, colinas y bosques dibujados como en un grabado). "Todo al azar" crea un continente nuevo. Cambiá una sola semilla y volvé a "Generar" para ver qué controla cada etapa. El estilo "Celdas" muestra la grilla del generador.
8. Con un mapa generado, "Ver celdas" en el lomo: hacé clic en el mapa para ver terreno, altura, temperatura, humedad y ríos de esa celda.
9. Shift + clic agrega o quita celdas y Shift + arrastrar las pinta. En su hoja podés cambiarles el terreno, subirlas, bajarlas, hacerlas tierra o agua, o restaurarlas. Los cambios se guardan y se mantienen al volver a generar con la misma semilla de continente.
10. En "Reinos" (en el lomo) creá un reino; con celdas elegidas, asignalas desde su hoja. En la tira de abajo del mapa, "Vista" muestra el mapa por reinos, altura, temperatura o humedad, con su leyenda.
11. Cada pin tiene un tipo con su ícono y un tamaño (chico, mediano, grande) que marca su importancia; la forma depende del grupo: insignia redonda para asentamientos, cuadrada para defensas y sin insignia para lugares menores. Tipos: ciudad, aldea, puerto, posada, fortaleza, torre, templo, bosque sagrado, cueva, mazmorra, mina, ruina, sitio abandonado, campo de batalla y otros. Con "Colocar pines" activo, la barra de íconos de arriba del mapa elige el tipo de los pines nuevos; en la ficha, "Cambiar" abre la grilla de tipos. Si un pin está dentro de un reino, su ficha permite marcarlo como capital: se dibuja con una estrella y un anillo doble.
12. En "Generar mapa", sección "Poblados", "Poblar mapa" funda ciudades, puertos, aldeas, fortalezas en las fronteras, cuevas, ruinas y bosques sagrados donde tiene sentido (ríos, costas, tierra fértil), con nombres inventados y su propia semilla. Puede crear reinos nuevos alrededor de las capitales o usar los que ya tenés. Los lugares que edites o muevas se conservan al volver a poblar.
13. Fichas del mundo: cada lugar, cada reino y cada personaje, facción o evento puede tener una ficha con texto. En la ficha de un pin o en "Fichas" (en el lomo), "Editar" permite escribir con formato simple: `# Título`, `**negrita**`, `*cursiva*`, `- lista`, `> cita` y enlaces `[[Otra ficha]]`. Tocar un enlace abre esa ficha y, si es un lugar o un reino, centra el mapa en él; un enlace a un nombre que todavía no tiene ficha la crea. Si renombrás un lugar, un reino o una ficha, la app ofrece cambiar los enlaces que usaban el nombre viejo. Cada ficha muestra qué otras la mencionan. Si borrás un lugar, su ficha se conserva como ficha suelta.

## Pruebas automáticas

Las pruebas están en `Tests/MapaMundos.Tests` (xUnit) y se corren con:

```
dotnet test Tests/MapaMundos.Tests
```

Cubren el generador (misma semilla, mismo mundo; porcentaje de tierra en las 10 plantillas; borde siempre mar), las ayudas de geometría de los dibujos, los retoques (una isla agregada es tierra y no cambia nada lejos) y la limpieza de parámetros guardados. Las "huellas" de `GeneratorTests` resumen tres mapas conocidos: si fallan después de un cambio, ese cambio altera los mapas que ya generaste con las mismas semillas.

## Cómo está organizado

- `Models/`: los datos (`MapProject`, `MapLayer`, `MapPin`) y el formato del archivo exportado.
- `Services/ProjectService.cs`: guarda el proyecto abierto y se encarga de persistir, exportar e importar.
- `Components/MapEditor.razor`: la interfaz principal (lomo, mapa, hoja de la ficha, pines).
- `Components/UiIcon.razor`: los íconos de la interfaz (modos, cerrar, subir, bajar), SVG propios.
- `Components/GeneratorPanel.razor`: el panel "Generar mapa".
- `Components/CellInfo.razor` y `CellPanel.razor`: la ficha de las celdas seleccionadas y las herramientas para editarlas.
- `Components/RegionPanel.razor`: la lista de reinos.
- `Models/WikiEntry.cs` y `Wiki/`: las fichas, el traductor del formato simple (`WikiMarkup`) y el índice de nombres y menciones (`WikiLinks`).
- `Components/WikiList.razor`, `EntrySheet.razor`, `EntryText.razor` y `WikiText.razor`: la lista de fichas, la ficha de un reino o suelta, el texto con su edición y el dibujo del texto con enlaces.
- `Components/PinIcon.razor`: los íconos de los tipos de pin (SVG propios).
- `Components/KindPicker.razor`: el selector visual de tipo (barra de colocación y ficha).
- `Components/TemplatePicker.razor`: la grilla de plantillas de relieve con miniaturas.
- `wwwroot/js/preview.js`: convierte los píxeles de una miniatura en imagen.
- `Models/Sketches.cs`: ayudas del dibujo (zonas dibujadas, validación, simplificación de Ramer-Douglas-Peucker, área, prueba del rayo).
- `Generation/SketchStage.cs`: aplica el clima y el bioma de las zonas de bioma dibujadas.
- `Generation/`: el generador de mapas, una clase por etapa (malla, continente, clima, ríos, biomas, poblados), las plantillas de relieve (`ReliefShape`), el dibujo en SVG, las vistas (`ThematicRenderer`) y los nombres (`NameGenerator`).
- `wwwroot/js/mapview.js`: zoom, arrastre y clics sobre el mapa.
- `wwwroot/js/storage.js` y `files.js`: IndexedDB y selector/descarga de archivos.

Decisiones de diseño que conviene entender:

- Los pines guardan su posición **normalizada** (0 a 1), no en píxeles.
- El zoom y el arrastre viven en JavaScript para que sean fluidos; C# solo recibe los eventos importantes.
- La imagen se guarda aparte del proyecto porque pesa mucho más.
- Estilo "atlas encuadernado": el lomo oscuro redefine los mismos tokens de color que usan las hojas claras (variables CSS con alcance), así cada botón y campo se pinta solo según dónde está.
- El zoom máximo depende de la imagen: 2x para PNG y JPG (más allá se ven borrosos) y 16x para SVG, que es vectorial y no pierde nitidez. Para mapas de Azgaar conviene exportar en SVG.
- El texto de las fichas nunca se inserta como HTML: el traductor lo separa en bloques y trozos, y Razor los escribe como texto. Así nada de lo que se escriba en una ficha puede ejecutarse en la página.

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

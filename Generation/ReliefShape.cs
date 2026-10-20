using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>Datos de cada plantilla para la interfaz: nombre, descripción y tierra sugerida.</summary>
public static class ReliefTemplates
{
    public static readonly (ReliefTemplate Template, string Name, string Hint, double LandRatio)[] All =
    {
        (ReliefTemplate.Continent, "Continente", "Una masa de tierra central", 0.40),
        (ReliefTemplate.Continents, "Continentes", "Dos o tres masas separadas por mar", 0.35),
        (ReliefTemplate.Pangea, "Pangea", "Un supercontinente con bahías", 0.55),
        (ReliefTemplate.Archipelago, "Archipiélago", "Muchas islas medianas y chicas", 0.20),
        (ReliefTemplate.VolcanicIsland, "Isla volcánica", "Una isla alta con islotes", 0.15),
        (ReliefTemplate.Peninsula, "Península", "Tierra que entra desde un borde", 0.35),
        (ReliefTemplate.Isthmus, "Istmo", "Dos masas unidas por una franja", 0.35),
        (ReliefTemplate.Mediterranean, "Mediterráneo", "Un mar interior rodeado de tierra", 0.60),
        (ReliefTemplate.Atoll, "Atolón", "Un anillo de islas con laguna", 0.05),
        (ReliefTemplate.Shattered, "Fragmentado", "Tierra rota en muchos pedazos", 0.35),

        // La tierra sale del dibujo: no hay porcentaje que proponer (0 = se calcula).
        (ReliefTemplate.Sketch, "Dibujo", "La costa que dibujes sobre el mapa", 0),
    };

    public static (ReliefTemplate Template, string Name, string Hint, double LandRatio) Info(ReliefTemplate template) =>
        All.First(t => t.Template == template);

    /// <summary>
    /// Miniatura de la plantilla: la misma forma que se generaría, calculada en una grilla chica
    /// en vez de en las celdas, con la tierra sugerida y los colores de la vista "Altura".
    /// Devuelve los píxeles como bytes RGBA (rojo, verde, azul, opacidad), fila por fila, que es
    /// el formato que entiende el canvas del navegador. landRatio: la tierra pedida (null = la
    /// sugerida por la plantilla; la usan las miniaturas de la grilla).
    /// </summary>
    public static byte[] Preview(ReliefTemplate template, int seed, int columns, int rows,
        IReadOnlyList<double[]>? sketch = null, double fidelity = 0.7, IReadOnlyList<SketchFeature>? features = null,
        double? landRatio = null)
    {
        var shape = ReliefShape.Create(template, seed, MapGenerator.Width, MapGenerator.Height, sketch, fidelity, features);

        // Centro de cada píxel de la miniatura, en píxeles del mapa, fila por fila.
        var points = new (double X, double Y)[columns * rows];
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < columns; c++)
                points[r * columns + c] = ((c + 0.5) / columns * MapGenerator.Width, (r + 0.5) / rows * MapGenerator.Height);
        }

        // Mismo nivel del mar, mismos retoques y misma escala que ReliefStage.
        var values = ReliefStage.Elevations(shape, points, null, landRatio ?? Info(template).LandRatio);

        var pixels = new byte[columns * rows * 4];
        for (var i = 0; i < values.Length; i++)
        {
            var elevation = values[i];
            var hex = ThematicRenderer.ElevationColor(elevation);
            pixels[i * 4] = Convert.ToByte(hex.Substring(1, 2), 16);
            pixels[i * 4 + 1] = Convert.ToByte(hex.Substring(3, 2), 16);
            pixels[i * 4 + 2] = Convert.ToByte(hex.Substring(5, 2), 16);
            pixels[i * 4 + 3] = 255;
        }
        return pixels;
    }
}

/// <summary>
/// La forma del relieve según la plantilla: altura = ruido + piezas - caída en los bordes.
///
/// Piezas (cada una ubicada con la semilla del continente):
/// - Mancha: una loma elíptica en forma de campana. Con altura negativa, un mar o una bahía.
/// - Cordillera: una loma a lo largo de un segmento. Montañas, istmos, el brazo de una
///   península; con altura negativa, un estrecho.
/// - Anillo: una loma en círculo con huecos. El atolón.
///
/// La altura que da esta clase no tiene escala fija: después ReliefStage elige el nivel del mar
/// para que la tierra ocupe el porcentaje pedido. Lo que importa es qué zonas quedan más altas.
/// La usan ReliefStage (en cada celda) y las miniaturas de la interfaz (en una grilla chica).
/// </summary>
public sealed class ReliefShape
{
    private const double EdgeDrop = 3;

    // ---- Piezas

    private abstract record Feature
    {
        public abstract double At(double x, double y);
    }

    /// <summary>Campana elíptica: vale Height en el centro y se apaga hacia afuera.</summary>
    private sealed record Blob(double Cx, double Cy, double Rx, double Ry, double Height) : Feature
    {
        public override double At(double x, double y)
        {
            var dx = (x - Cx) / Rx;
            var dy = (y - Cy) / Ry;
            return Height * Math.Exp(-(dx * dx + dy * dy));
        }
    }

    /// <summary>Loma a lo largo del segmento de A a B: depende de la distancia al segmento.</summary>
    private sealed record Ridge(double Ax, double Ay, double Bx, double By, double Width, double Height) : Feature
    {
        public override double At(double x, double y)
        {
            // Punto del segmento más cercano: se proyecta sobre la recta y se limita a [A, B].
            var vx = Bx - Ax;
            var vy = By - Ay;
            var t = Math.Clamp(((x - Ax) * vx + (y - Ay) * vy) / (vx * vx + vy * vy), 0, 1);
            var dx = x - (Ax + t * vx);
            var dy = y - (Ay + t * vy);
            var d = Math.Sqrt(dx * dx + dy * dy) / Width;
            return Height * Math.Exp(-d * d);
        }
    }

    /// <summary>Loma en círculo; cerca de los ángulos de Gaps se apaga (los pasos al mar).</summary>
    private sealed record Ring(double Cx, double Cy, double Radius, double Width, double Height, double[] Gaps) : Feature
    {
        private const double GapSize = 0.3; // radianes

        public override double At(double x, double y)
        {
            var dx = x - Cx;
            var dy = y - Cy;
            var d = (Math.Sqrt(dx * dx + dy * dy) - Radius) / Width;
            var value = Height * Math.Exp(-d * d);

            var angle = Math.Atan2(dy, dx);
            foreach (var gap in Gaps)
            {
                // Distancia angular, teniendo en cuenta que -π y π son el mismo ángulo.
                var a = Math.Abs(Math.IEEERemainder(angle - gap, 2 * Math.PI));
                if (a < GapSize) value *= a / GapSize * (a / GapSize);
            }
            return value;
        }
    }

    // ---- Estado

    private readonly double _width;
    private readonly double _height;
    private readonly PerlinNoise _noise;
    private readonly List<Feature> _features = new();
    private double _frequency = 3;
    private double _noiseWeight = 0.5;
    private double _offset;            // suma fija (el Mediterráneo parte de tierra en todo el mapa)
    private bool _openTop, _openRight, _openBottom, _openLeft; // bordes por donde la tierra sigue

    // Forma original (plantilla Continente): se conserva tal cual para que los mapas guardados
    // antes de las plantillas se sigan regenerando idénticos.
    private bool _legacy;
    private double _centerX = 0.5, _centerY = 0.5;

    // ---- Dibujo (plantilla Sketch)

    // Distancia (en píxeles del mapa) que vale "1" en la fórmula. Con 60, la fuerza del dibujo
    // crece a lo largo de unos 60 px a cada lado de la línea.
    private const double SketchScale = 60;

    // Adentro, la fuerza se satura enseguida: como mucho 0.4 (24 px de la línea) y nunca más de
    // 0.6 en valor ("la meseta"). Si siguiera creciendo, todo el interior quedaría altísimo al
    // reescalar y saldría montaña de punta a punta; en las pruebas, sin el tope de 0.6, la mitad
    // de la tierra era montaña con fidelidad 100 %. Afuera baja hasta -3: mar seguro lejos de la línea.
    private const double SketchInsideCap = 0.4;
    private const double SketchPlateau = 0.6;
    private const double SketchOutsideCap = -3;

    private List<(double X, double Y)[]>? _islands;
    private List<(double MinX, double MinY, double MaxX, double MaxY)> _boxes = new();
    private double _sketchStrength;

    // Zonas de agua dibujadas ("islas negativas") y cordilleras dibujadas (líneas abiertas).
    private List<(double X, double Y)[]> _water = new();
    private List<(double MinX, double MinY, double MaxX, double MaxY)> _waterBoxes = new();
    private List<(double X, double Y)[]> _ranges = new();

    // Cordillera dibujada: altura y ancho de la loma (en píxeles del mapa) a lo largo de la línea.
    private const double RangeHeight = 0.9;
    private const double RangeWidth = 40;

    // ---- Retoques dibujados sobre las otras plantillas (ver Overlay)

    // Distancia afuera de una isla agregada (en píxeles del mapa) donde su efecto ya es 0.
    private const double RetouchFadeEnd = 240;

    // Altura de una isla agregada: su punto más alto llega a RetouchPeak de la cima de la
    // plantilla (0.55: colinas, no montañas; las montañas se dibujan aparte). RetouchDetailPeak
    // es lo más alto que suele dar el ruido de detalle (medido: 95 % de los valores por debajo).
    private const double RetouchPeak = 0.55;
    private const double RetouchDetailPeak = 0.3;

    private List<(double X, double Y)[]> _extraIslands = new();
    private List<(double MinX, double MinY, double MaxX, double MaxY)> _extraBoxes = new();
    private PerlinNoise? _overlayNoise; // detalle de las costas retocadas (null: no hay retoques)

    /// <summary>
    /// True si el nivel del mar es fijo (altura 0) en vez de elegirse por porcentaje de tierra.
    /// Pasa con el dibujo: la tierra es la que dibujó el autor, no un porcentaje pedido.
    /// </summary>
    public bool FixedSeaLevel => _islands is not null;

    /// <summary>
    /// Desde qué altura cruda se mide el relieve de la tierra (0 en las plantillas comunes). Con
    /// el dibujo es la meseta: si se midiera desde la costa, la meseta pondría "alto" a todo el
    /// interior y saldría montaña por todos lados.
    /// </summary>
    public double LandBase => _islands is null ? 0 : Math.Min(_sketchStrength * SketchInsideCap, SketchPlateau);

    private ReliefShape(double width, double height, int seed)
    {
        _width = width;
        _height = height;
        _noise = new PerlinNoise(seed);
    }

    /// <summary>Altura sin escala en el punto (x, y) en píxeles del mapa.</summary>
    public double Height(double x, double y)
    {
        // Coordenadas del ruido divididas por el alto: los rasgos no se estiran aunque el mapa
        // sea más ancho que alto.
        var nx = x / _height * _frequency;
        var ny = y / _height * _frequency;

        if (_legacy)
        {
            var dx = (x / _width - _centerX) * 2;
            var dy = (y / _height - _centerY) * 2;
            var edge = Math.Pow(Math.Max(Math.Abs(dx), Math.Abs(dy)), 8);
            return _noise.Fractal(nx, ny, 5) - 0.55 * (dx * dx + dy * dy) - EdgeDrop * edge;
        }

        if (_islands is not null)
        {
            // Dibujo: fuerza × distancia con signo + ruido. La costa queda donde la suma vale 0.
            // Con fuerza alta, el ruido apenas la corre de la línea; con fuerza baja, la inventa.
            var pull = _sketchStrength * SignedDistance(_islands, _boxes, x, y) / SketchScale;
            pull = Math.Clamp(pull, _sketchStrength * SketchOutsideCap,
                Math.Min(_sketchStrength * SketchInsideCap, SketchPlateau));

            // Agua dibujada: una "isla negativa". Adentro del agua la fuerza baja como bajaría
            // afuera de una isla; se toma el mínimo, así el agua gana sobre la tierra y su orilla
            // queda sobre la línea. Va con el doble de fuerza: los lagos son chicos, y la misma
            // tolerancia de la costa (unos 15 px) dejaba un cuarto de un lago dibujado como tierra.
            if (_water.Count > 0)
            {
                var waterStrength = 2 * _sketchStrength;
                var waterDistance = SignedDistance(_water, _waterBoxes, x, y);
                pull = Math.Min(pull, Math.Max(-waterStrength * waterDistance / SketchScale, waterStrength * SketchOutsideCap));
            }

            // Cordilleras dibujadas: una loma a lo largo de cada línea.
            var ridges = 0.0;
            foreach (var range in _ranges)
            {
                var d = Sketches.DistanceToPolyline(range, x, y) / RangeWidth;
                if (d < 3) ridges += RangeHeight * Math.Exp(-d * d); // más lejos vale casi 0
            }

            return pull + ridges + _noise.Fractal(nx, ny, 5) - EdgeDrop * EdgeTerm(x, y);
        }

        var value = _offset + _noiseWeight * _noise.Fractal(nx, ny, 5);
        foreach (var feature in _features)
            value += feature.At(x, y);
        return value - EdgeDrop * EdgeTerm(x, y);
    }

    /// <summary>
    /// Retoques dibujados encima de una plantilla común (no de la plantilla Dibujo, donde el
    /// dibujo ya es la base). Se aplican después de elegir el nivel del mar con la plantilla
    /// sola, y se miden desde ese nivel: así una isla agregada se suma, en lugar de que el
    /// porcentaje de tierra hunda tierra en otro lado para compensarla.
    /// - Tierra: gana donde la isla dibujada es más alta que el relieve (el mar pasa a tierra).
    /// - Agua: gana donde es más baja (la tierra pasa a agua: borrar, lagos, bahías).
    /// - Cordilleras: se suman al relieve.
    /// landHeight es la altura de la cima de la plantilla sobre el mar: los retoques se miden
    /// con esa vara para que su relieve se parezca al del resto del mapa.
    /// </summary>
    public double Overlay(double x, double y, double raw, double seaLevel, double landHeight)
    {
        if (_islands is not null || _overlayNoise is null) return raw;

        var v = raw - seaLevel; // altura sobre el mar: positiva es tierra
        var detail = _overlayNoise.Fractal(x / _height * 3, y / _height * 3, 5); // costas irregulares

        if (_extraIslands.Count > 0)
        {
            // La costa sale igual que con la plantilla Dibujo: donde fuerza + ruido vale 0.
            var distance = SignedDistance(_extraIslands, _extraBoxes, x, y);
            var cap = Math.Min(_sketchStrength * SketchInsideCap, SketchPlateau);
            var pull = Math.Clamp(_sketchStrength * distance / SketchScale, _sketchStrength * SketchOutsideCap, cap);

            // La altura, en cambio, se lleva a la escala de la plantilla (multiplicar no mueve el
            // 0, así que la costa no cambia). Sin esto, la meseta de Dibujo quedaba a altura de
            // montaña: en la prueba, el centro de una isla chica salía con nieves eternas.
            var island = (pull + detail) / (cap + RetouchDetailPeak) * RetouchPeak * landHeight;

            // Alcance: el retoque pesa entero hasta 60 px afuera de la línea y se desvanece hasta
            // 240 px. Sin esto, con fidelidad baja el ruido levantaba islotes en cualquier parte.
            var t = Math.Clamp((distance + RetouchFadeEnd) / (RetouchFadeEnd - SketchScale), 0, 1);
            var weight = t * t * (3 - 2 * t); // "smoothstep": arranca y termina suave, sin escalones
            v += weight * (Math.Max(v, island) - v);
        }

        if (_water.Count > 0)
        {
            var waterStrength = 2 * _sketchStrength;
            var waterPull = Math.Max(-waterStrength * SignedDistance(_water, _waterBoxes, x, y) / SketchScale,
                waterStrength * SketchOutsideCap);
            v = Math.Min(v, waterPull + detail);
        }

        // Cordilleras: la misma loma que en Dibujo, medida con la cima de la plantilla.
        foreach (var range in _ranges)
        {
            var d = Sketches.DistanceToPolyline(range, x, y) / RangeWidth;
            if (d < 3) v += RangeHeight * landHeight * Math.Exp(-d * d);
        }

        return v + seaLevel;
    }

    // Caída brusca solo muy cerca de los bordes cerrados (potencia 8: casi 0 hasta 0.7 de la
    // distancia al borde). Los bordes abiertos no tienen caída: la tierra sigue fuera del mapa.
    private double EdgeTerm(double x, double y)
    {
        var ex = (x / _width - 0.5) * 2;
        var ey = (y / _height - 0.5) * 2;
        var left = ex < 0 && !_openLeft ? -ex : 0;
        var right = ex > 0 && !_openRight ? ex : 0;
        var top = ey < 0 && !_openTop ? -ey : 0;
        var bottom = ey > 0 && !_openBottom ? ey : 0;
        return Math.Pow(Math.Max(Math.Max(left, right), Math.Max(top, bottom)), 8);
    }

    // ------------------------------------------------------------- plantillas

    /// <summary>
    /// Distancia con signo a un grupo de polígonos (las islas, o las zonas de agua): positiva
    /// adentro de alguno, negativa afuera, y su valor absoluto es la distancia a la línea más
    /// cercana. Adentro o afuera se decide con la prueba del rayo (la misma que usa
    /// Mesh.FindCell): se cuentan los lados que cruza una línea horizontal desde el punto.
    /// </summary>
    private static double SignedDistance(List<(double X, double Y)[]> polygons,
        List<(double MinX, double MinY, double MaxX, double MaxY)> boxes, double x, double y)
    {
        var inside = false;
        var best = double.MaxValue;

        for (var k = 0; k < polygons.Count; k++)
        {
            var island = polygons[k];
            var box = boxes[k];

            // Si el punto queda lejos de la caja que rodea a la isla, ninguno de sus lados
            // puede estar más cerca que lo ya encontrado ni contener al punto: se saltea entera.
            var boxDx = Math.Max(Math.Max(box.MinX - x, x - box.MaxX), 0);
            var boxDy = Math.Max(Math.Max(box.MinY - y, y - box.MaxY), 0);
            if (boxDx * boxDx + boxDy * boxDy > best * best) continue;

            for (int a = 0, b = island.Length - 1; a < island.Length; b = a++)
            {
                var (xa, ya) = island[a];
                var (xb, yb) = island[b];
                if ((ya > y) != (yb > y) && x < (xb - xa) * (y - ya) / (yb - ya) + xa)
                    inside = !inside;
                best = Math.Min(best, Sketches.DistanceToSegment((x, y), (xa, ya), (xb, yb)));
            }
        }

        return inside ? best : -best;
    }

    /// <summary>Arma la forma de la plantilla. La misma semilla da siempre la misma forma.</summary>
    public static ReliefShape Create(ReliefTemplate template, int seed, double width, double height,
        IReadOnlyList<double[]>? sketch = null, double fidelity = 0.7, IReadOnlyList<SketchFeature>? features = null)
    {
        var shape = new ReliefShape(width, height, seed);
        var rng = new Rng(seed + 1);
        var w = width;
        var h = height;

        switch (template)
        {
            case ReliefTemplate.Continents:
            {
                var count = 2 + rng.NextInt(2);
                for (var i = 0; i < count; i++)
                {
                    var cx = w * (i + 0.5) / count + rng.Range(-0.05, 0.05) * w;
                    var cy = h * (0.5 + rng.Range(-0.12, 0.12));
                    var rx = w / count * rng.Range(0.28, 0.34);
                    shape.Add(new Blob(cx, cy, rx, h * rng.Range(0.26, 0.34), 1.0));
                    shape.AddRidgeThrough(rng, cx, cy, rx * 0.7, 35, 0.45);
                }

                // Canales de mar entre continentes: sin ellos, las manchas se tocan y queda uno solo.
                for (var i = 1; i < count; i++)
                {
                    var x = w * i / count + rng.Range(-0.03, 0.03) * w;
                    shape.Add(new Ridge(x + rng.Range(-60, 60), -50, x + rng.Range(-60, 60), h + 50, 70, -1.0));
                }
                shape.Noise(rng.Range(2.4, 3.2), 0.55);
                break;
            }

            case ReliefTemplate.Pangea:
            {
                var cx = w * (0.5 + rng.Range(-0.05, 0.05));
                var cy = h * (0.5 + rng.Range(-0.05, 0.05));
                shape.Add(new Blob(cx, cy, w * 0.36, h * 0.38, 1.2));
                for (var i = 0; i < 3; i++)
                    shape.AddRidgeThrough(rng, cx + rng.Range(-0.2, 0.2) * w, cy + rng.Range(-0.2, 0.2) * h, h * 0.25, 40, 0.5);

                // Bahías: manchas negativas sobre el borde del supercontinente.
                for (var i = 0; i < 2; i++)
                {
                    var a = rng.Range(0, 2 * Math.PI);
                    var r = rng.Range(90, 140);
                    shape.Add(new Blob(cx + Math.Cos(a) * w * 0.3, cy + Math.Sin(a) * h * 0.32, r, r, -0.8));
                }
                shape.Noise(rng.Range(2.4, 3.0), 0.5);
                break;
            }

            case ReliefTemplate.Archipelago:
            {
                foreach (var (x, y) in SpacedPoints(rng, w, h, count: 14 + rng.NextInt(6), gap: 150, margin: 0.12))
                {
                    var ry = rng.Range(35, 95);
                    shape.Add(new Blob(x, y, ry * rng.Range(0.8, 1.3), ry, rng.Range(0.6, 1.0)));
                }
                shape.Noise(rng.Range(3.5, 4.5), 0.55);
                break;
            }

            case ReliefTemplate.VolcanicIsland:
            {
                var cx = w * (0.5 + rng.Range(-0.08, 0.08));
                var cy = h * (0.5 + rng.Range(-0.08, 0.08));
                var radius = h * 0.28;
                shape.Add(new Blob(cx, cy, radius * 1.1, radius, 0.8));
                shape.Add(new Blob(cx, cy, 55, 55, 1.2)); // el cono del volcán

                for (var i = 0; i < 3; i++)
                {
                    var a = rng.Range(0, 2 * Math.PI);
                    var d = radius * rng.Range(1.4, 1.8);
                    var r = rng.Range(30, 55);
                    shape.Add(new Blob(cx + Math.Cos(a) * d, cy + Math.Sin(a) * d, r, r, rng.Range(0.5, 0.8)));
                }
                shape.Noise(3, 0.4);
                break;
            }

            case ReliefTemplate.Peninsula:
            {
                // Lado por donde entra la tierra: 0 arriba, 1 derecha, 2 abajo, 3 izquierda.
                var side = rng.NextInt(4);
                var (sx, sy, dirX, dirY, length) = side switch
                {
                    0 => (w * rng.Range(0.35, 0.65), 0.0, 0.0, 1.0, h * 0.65),
                    1 => (w, h * rng.Range(0.35, 0.65), -1.0, 0.0, w * 0.5),
                    2 => (w * rng.Range(0.35, 0.65), h, 0.0, -1.0, h * 0.65),
                    _ => (0.0, h * rng.Range(0.35, 0.65), 1.0, 0.0, w * 0.5),
                };

                // El brazo se inclina hasta 25 grados para no quedar siempre recto.
                var tilt = rng.Range(-0.45, 0.45);
                var ex = sx + (dirX * Math.Cos(tilt) - dirY * Math.Sin(tilt)) * length;
                var ey = sy + (dirX * Math.Sin(tilt) + dirY * Math.Cos(tilt)) * length;

                shape.Add(new Blob(sx - dirX * h * 0.2, sy - dirY * h * 0.2, h * 0.45, h * 0.45, 1.1)); // tierra firme
                shape.Add(new Ridge(sx, sy, ex, ey, h * 0.13, 1.0));                                     // el brazo
                shape.Add(new Ridge(sx, sy, sx + (ex - sx) * 0.85, sy + (ey - sy) * 0.85, 30, 0.5));      // su cordillera
                shape.Add(new Blob(ex, ey, h * 0.1, h * 0.1, 0.6));                                      // la punta

                shape._openTop = side == 0;
                shape._openRight = side == 1;
                shape._openBottom = side == 2;
                shape._openLeft = side == 3;
                shape.Noise(3, 0.5);
                break;
            }

            case ReliefTemplate.Isthmus:
            {
                var ax = w * (0.25 + rng.Range(-0.04, 0.04));
                var ay = h * (0.5 + rng.Range(-0.12, 0.12));
                var bx = w * (0.75 + rng.Range(-0.04, 0.04));
                var by = h * (0.5 + rng.Range(-0.12, 0.12));
                shape.Add(new Blob(ax, ay, w * 0.17, h * 0.3, 1.0));
                shape.Add(new Blob(bx, by, w * 0.17, h * 0.3, 1.0));
                shape.Add(new Ridge(ax, ay, bx, by, 45, 0.95)); // la franja que las une
                shape.Noise(3, 0.45);
                break;
            }

            case ReliefTemplate.Mediterranean:
            {
                // Tierra en todo el mapa (bordes abiertos) y un mar grande en el medio.
                shape._offset = 0.9;
                shape._openTop = shape._openRight = shape._openBottom = shape._openLeft = true;

                var cx = w * (0.5 + rng.Range(-0.05, 0.05));
                var cy = h * (0.5 + rng.Range(-0.05, 0.05));
                shape.Add(new Blob(cx, cy, w * 0.30, h * 0.24, -1.9));
                shape.Add(new Blob(cx + rng.Range(-0.12, 0.12) * w, cy + rng.Range(-0.06, 0.06) * h, w * 0.16, h * 0.14, -1.2));

                // Estrecho hasta un borde: sin salida al mar abierto, el generador lo trataría
                // como un lago (es la definición de lago que usa ReliefStage).
                var toRight = rng.NextDouble() < 0.5;
                shape.Add(new Ridge(cx, cy, toRight ? w + 50 : -50, cy + rng.Range(-0.1, 0.1) * h, 30, -2.2));

                shape.Add(new Ridge(w * 0.15, h * 0.12, w * 0.6, h * 0.08, 40, 0.5));   // cordillera del norte
                shape.Add(new Ridge(w * 0.4, h * 0.9, w * 0.85, h * 0.88, 40, 0.5));   // cordillera del sur
                shape.Noise(3, 0.5);
                break;
            }

            case ReliefTemplate.Atoll:
            {
                var cx = w * (0.5 + rng.Range(-0.05, 0.05));
                var cy = h * (0.5 + rng.Range(-0.05, 0.05));
                var radius = h * rng.Range(0.28, 0.34);
                var gaps = Enumerable.Range(0, 3 + rng.NextInt(3)).Select(_ => rng.Range(-Math.PI, Math.PI)).ToArray();
                shape.Add(new Ring(cx, cy, radius, 26, 1.0, gaps));
                shape.Add(new Blob(cx, cy, radius * 0.8, radius * 0.8, -0.6)); // la laguna
                if (rng.NextDouble() < 0.5) shape.Add(new Blob(cx, cy, 40, 40, 0.9)); // islote central
                shape.Noise(rng.Range(5, 7), 0.25);
                break;
            }

            case ReliefTemplate.Shattered:
            {
                foreach (var (x, y) in SpacedPoints(rng, w, h, count: 28 + rng.NextInt(10), gap: 90, margin: 0.08))
                {
                    var r = rng.Range(30, 90);
                    shape.Add(new Blob(x, y, r, r * rng.Range(0.7, 1.3), rng.Range(0.5, 1.0)));
                }
                shape.Noise(rng.Range(5, 6.5), 0.9);
                break;
            }

            case ReliefTemplate.Sketch:
            {
                // Las islas vienen normalizadas (0 a 1): se pasan a píxeles del mapa.
                shape._islands = (sketch ?? Array.Empty<double[]>())
                    .Select(flat => Enumerable.Range(0, flat.Length / 2)
                        .Select(i => (flat[i * 2] * w, flat[i * 2 + 1] * h))
                        .ToArray())
                    .Where(island => island.Length >= 3)
                    .ToList();
                shape._boxes = shape._islands.Select(BoundingBox).ToList();

                // Agua y cordilleras dibujadas. Los biomas no cambian el relieve: los aplica SketchStage.
                var drawn = features ?? Array.Empty<SketchFeature>();
                shape._water = drawn.Where(f => f.Kind == SketchFeatureKind.Water)
                    .Select(f => Sketches.ToPixels(f.Points, w, h)).Where(p => p.Length >= 3).ToList();
                shape._waterBoxes = shape._water.Select(BoundingBox).ToList();
                shape._ranges = drawn.Where(f => f.Kind == SketchFeatureKind.Mountains)
                    .Select(f => Sketches.ToPixels(f.Points, w, h)).Where(p => p.Length >= 2).ToList();

                // Fuerza del dibujo según la fidelidad. Con el cuadrado, el deslizador reparte
                // mejor los efectos: 0 % no pesa (0.02), 10 % casi nada (0.04), 50 % deja una costa libre
                // (0.52) y 100 % la ata a la línea (2.02: el ruido la corre unos 15 px).
                fidelity = Math.Clamp(fidelity, 0, 1);
                shape._sketchStrength = 0.02 + 2 * fidelity * fidelity;
                shape._frequency = rng.Range(2.5, 3.5);
                break;
            }

            default: // Continent: la forma original, sin cambios.
                shape._legacy = true;
                shape._centerX = 0.5 + rng.Range(-0.08, 0.08);
                shape._centerY = 0.5 + rng.Range(-0.08, 0.08);
                shape._frequency = rng.Range(2.0, 3.2);
                break;
        }

        // Retoques dibujados encima de las otras plantillas. Se arman después de la plantilla y
        // con su propio ruido, así no cambian ningún número al azar de la forma: un mapa sin
        // dibujos sale idéntico a antes.
        var drawnIslands = sketch ?? Array.Empty<double[]>();
        var drawnFeatures = features ?? Array.Empty<SketchFeature>();
        if (template != ReliefTemplate.Sketch && (drawnIslands.Count > 0 || drawnFeatures.Count > 0))
        {
            shape._extraIslands = drawnIslands.Select(flat => Sketches.ToPixels(flat, w, h)).Where(p => p.Length >= 3).ToList();
            shape._extraBoxes = shape._extraIslands.Select(BoundingBox).ToList();
            shape._water = drawnFeatures.Where(f => f.Kind == SketchFeatureKind.Water)
                .Select(f => Sketches.ToPixels(f.Points, w, h)).Where(p => p.Length >= 3).ToList();
            shape._waterBoxes = shape._water.Select(BoundingBox).ToList();
            shape._ranges = drawnFeatures.Where(f => f.Kind == SketchFeatureKind.Mountains)
                .Select(f => Sketches.ToPixels(f.Points, w, h)).Where(p => p.Length >= 2).ToList();

            fidelity = Math.Clamp(fidelity, 0, 1);
            shape._sketchStrength = 0.02 + 2 * fidelity * fidelity;
            shape._overlayNoise = new PerlinNoise(seed + 3);
        }

        return shape;
    }

    // --------------------------------------------------------------- ayudas

    private void Add(Feature feature) => _features.Add(feature);

    // Caja que rodea a un polígono: permite saltear rápido los que quedan lejos de un punto.
    private static (double MinX, double MinY, double MaxX, double MaxY) BoundingBox((double X, double Y)[] polygon) =>
        (polygon.Min(p => p.X), polygon.Min(p => p.Y), polygon.Max(p => p.X), polygon.Max(p => p.Y));

    private void Noise(double frequency, double weight)
    {
        _frequency = frequency;
        _noiseWeight = weight;
    }

    /// <summary>Cordillera centrada en (cx, cy), con ángulo al azar y medio largo "half".</summary>
    private void AddRidgeThrough(Rng rng, double cx, double cy, double half, double width, double height)
    {
        var a = rng.Range(0, Math.PI);
        Add(new Ridge(cx - Math.Cos(a) * half, cy - Math.Sin(a) * half, cx + Math.Cos(a) * half, cy + Math.Sin(a) * half, width, height));
    }

    /// <summary>
    /// Puntos al azar dentro del mapa (sin acercarse a los bordes más de "margin") y separados
    /// al menos "gap" píxeles: se prueba un punto y se descarta si cae cerca de otro.
    /// </summary>
    private static List<(double X, double Y)> SpacedPoints(Rng rng, double w, double h, int count, double gap, double margin)
    {
        var points = new List<(double X, double Y)>();
        for (var attempt = 0; attempt < count * 40 && points.Count < count; attempt++)
        {
            var x = rng.Range(margin, 1 - margin) * w;
            var y = rng.Range(margin, 1 - margin) * h;
            if (points.All(p => (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) >= gap * gap))
                points.Add((x, y));
        }
        return points;
    }
}

namespace MapaMundos.Models;

/// <summary>Herramienta de dibujo elegida en la barra de "Dibujar costa".</summary>
public enum SketchTool
{
    Land,       // contorno cerrado: una isla
    Water,      // contorno cerrado: lago, bahía o mar interior
    Mountains,  // línea abierta: una cordillera
    Biome,      // contorno cerrado: una zona con el bioma elegido
    Eraser      // clic sobre un trazo: lo borra
}

/// <summary>Tipo de una zona dibujada que no es tierra (la tierra va en la lista de islas).</summary>
public enum SketchFeatureKind
{
    Water,
    Mountains,
    Biome
}

/// <summary>
/// Una zona dibujada: agua, cordillera o bioma. Points es la lista plana normalizada
/// [x0, y0, x1, y1, ...] como en las islas; en las cordilleras es una línea abierta.
/// </summary>
public class SketchFeature
{
    public SketchFeatureKind Kind { get; set; }

    /// <summary>Bioma de la zona (solo para Kind = Biome).</summary>
    public Biome? Biome { get; set; }

    public double[] Points { get; set; } = Array.Empty<double>();

    public SketchFeature Copy() => new() { Kind = Kind, Biome = Biome, Points = (double[])Points.Clone() };
}

/// <summary>
/// Ayudas para los dibujos de costa. Un dibujo es una lista de islas; cada isla es una lista
/// plana de coordenadas normalizadas [x0, y0, x1, y1, ...] entre 0 y 1 (como los pines, así no
/// depende del tamaño del mapa). La isla se cierra sola: el último punto se une con el primero.
/// </summary>
public static class Sketches
{
    /// <summary>
    /// Deja solo islas válidas: al menos 3 puntos, una cantidad par de números y todos entre
    /// 0 y 1. Se usa al cargar o importar, porque el archivo pudo editarse a mano.
    /// </summary>
    public static List<double[]> Clean(List<double[]>? sketch) =>
        (sketch ?? new())
            .Where(island => island is { Length: >= 6 } && island.Length % 2 == 0 && island.All(v => !double.IsNaN(v)))
            .Select(island => island.Select(v => Math.Clamp(v, 0, 1)).ToArray())
            .ToList();

    public static List<double[]> Copy(IEnumerable<double[]> sketch) =>
        sketch.Select(island => (double[])island.Clone()).ToList();

    /// <summary>True si los dos dibujos son iguales, isla por isla y número por número.</summary>
    public static bool SameAs(IReadOnlyList<double[]> a, IReadOnlyList<double[]> b) =>
        a.Count == b.Count && a.Zip(b).All(pair => pair.First.SequenceEqual(pair.Second));

    /// <summary>
    /// Deja solo zonas válidas: tipo conocido, puntos entre 0 y 1, al menos 3 puntos en las
    /// zonas cerradas y 2 en las cordilleras, y un bioma de tierra en las zonas de bioma.
    /// </summary>
    public static List<SketchFeature> CleanFeatures(List<SketchFeature>? features) =>
        (features ?? new())
            .Where(f => f is not null && Enum.IsDefined(f.Kind) && f.Points is not null && f.Points.Length % 2 == 0)
            .Where(f => f.Points.Length >= (f.Kind == SketchFeatureKind.Mountains ? 4 : 6) && f.Points.All(v => !double.IsNaN(v)))
            .Where(f => f.Kind != SketchFeatureKind.Biome || (f.Biome is { } b && BiomeNames.Land.Contains(b)))
            .Select(f => new SketchFeature
            {
                Kind = f.Kind,
                Biome = f.Kind == SketchFeatureKind.Biome ? f.Biome : null,
                Points = f.Points.Select(v => Math.Clamp(v, 0, 1)).ToArray()
            })
            .ToList();

    public static List<SketchFeature> CopyFeatures(IEnumerable<SketchFeature> features) =>
        features.Select(f => f.Copy()).ToList();

    public static bool SameFeatures(IReadOnlyList<SketchFeature> a, IReadOnlyList<SketchFeature> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.Kind == p.Second.Kind && p.First.Biome == p.Second.Biome && p.First.Points.SequenceEqual(p.Second.Points));

    /// <summary>Pasa una lista plana normalizada a puntos en píxeles del mapa.</summary>
    public static (double X, double Y)[] ToPixels(double[] flat, double width, double height) =>
        Enumerable.Range(0, flat.Length / 2).Select(i => (flat[i * 2] * width, flat[i * 2 + 1] * height)).ToArray();

    /// <summary>Prueba del rayo: true si el punto está adentro del polígono (cerrado).</summary>
    public static bool Contains(IReadOnlyList<(double X, double Y)> polygon, double x, double y)
    {
        var inside = false;
        for (int a = 0, b = polygon.Count - 1; a < polygon.Count; b = a++)
        {
            var (xa, ya) = polygon[a];
            var (xb, yb) = polygon[b];
            if ((ya > y) != (yb > y) && x < (xb - xa) * (y - ya) / (yb - ya) + xa)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Distancia de un punto a una línea abierta (la cordillera): el tramo más cercano.</summary>
    public static double DistanceToPolyline(IReadOnlyList<(double X, double Y)> line, double x, double y)
    {
        var best = double.MaxValue;
        for (var i = 0; i < line.Count - 1; i++)
            best = Math.Min(best, DistanceToSegment((x, y), line[i], line[i + 1]));
        return best;
    }

    /// <summary>Largo total de una línea abierta, en las mismas unidades que sus puntos.</summary>
    public static double Length(IReadOnlyList<(double X, double Y)> line)
    {
        var total = 0.0;
        for (var i = 0; i < line.Count - 1; i++)
        {
            var dx = line[i + 1].X - line[i].X;
            var dy = line[i + 1].Y - line[i].Y;
            total += Math.Sqrt(dx * dx + dy * dy);
        }
        return total;
    }

    /// <summary>
    /// Simplificación de Ramer-Douglas-Peucker. Un trazo a mano tiene cientos de puntos casi
    /// alineados; este algoritmo deja solo los que cambian la forma:
    /// 1. Se traza la recta entre el primer y el último punto.
    /// 2. Se busca el punto más alejado de esa recta.
    /// 3. Si está a menos de "tolerance", todos los del medio sobran. Si no, ese punto se queda,
    ///    y se repite lo mismo con cada mitad.
    /// Los puntos vienen en píxeles, para que la tolerancia sea una distancia real en el mapa.
    /// </summary>
    public static List<(double X, double Y)> Simplify(IReadOnlyList<(double X, double Y)> points, double tolerance)
    {
        if (points.Count < 3) return points.ToList();

        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        var pending = new Stack<(int First, int Last)>();
        pending.Push((0, points.Count - 1));

        // Con una pila en vez de recursión: un trazo muy largo no agota la memoria de llamadas.
        while (pending.Count > 0)
        {
            var (first, last) = pending.Pop();
            var farthest = -1;
            var farthestDistance = 0.0;
            for (var i = first + 1; i < last; i++)
            {
                var d = DistanceToSegment(points[i], points[first], points[last]);
                if (d > farthestDistance)
                {
                    farthestDistance = d;
                    farthest = i;
                }
            }

            if (farthest >= 0 && farthestDistance > tolerance)
            {
                keep[farthest] = true;
                pending.Push((first, farthest));
                pending.Push((farthest, last));
            }
        }

        return points.Where((_, i) => keep[i]).ToList();
    }

    /// <summary>
    /// Área de un polígono con la fórmula del "cordón de zapato" (shoelace): se suman los
    /// productos cruzados de cada lado. Sirve para descartar trazos que son casi una línea.
    /// </summary>
    public static double Area(IReadOnlyList<(double X, double Y)> polygon)
    {
        var sum = 0.0;
        for (var i = 0; i < polygon.Count; i++)
        {
            var (x1, y1) = polygon[i];
            var (x2, y2) = polygon[(i + 1) % polygon.Count];
            sum += x1 * y2 - x2 * y1;
        }
        return Math.Abs(sum) / 2;
    }

    /// <summary>Distancia de un punto al segmento AB (la usan la simplificación y el relieve).</summary>
    public static double DistanceToSegment((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        var vx = b.X - a.X;
        var vy = b.Y - a.Y;
        var length2 = vx * vx + vy * vy;
        var t = length2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * vx + (p.Y - a.Y) * vy) / length2, 0, 1);
        var dx = p.X - (a.X + t * vx);
        var dy = p.Y - (a.Y + t * vy);
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

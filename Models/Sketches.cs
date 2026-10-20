namespace MapaMundos.Models;

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

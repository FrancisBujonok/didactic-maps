namespace MapaMundos.Generation;

/// <summary>
/// La malla del mapa: miles de celdas poligonales irregulares, cada una con sus vecinas.
/// Todas las etapas (relieve, clima, ríos, biomas) trabajan sobre estas celdas.
///
/// Cómo se arma:
/// 1. Puntos en una grilla hexagonal (las filas impares corridas media separación).
/// 2. Cada punto se desplaza un poco al azar para que las celdas no se vean regulares.
/// 3. En una grilla hexagonal cada punto tiene 6 vecinas que se conocen por fila y columna,
///    así que no hace falta un algoritmo de triangulación.
/// 4. El polígono de una celda une los baricentros de los 6 triángulos que forma con
///    cada par de vecinas consecutivas. Dos celdas vecinas comparten exactamente esos vértices.
/// </summary>
public sealed class Mesh
{
    // Desplazamiento máximo de cada punto, como fracción de la separación. Con 0.25 los
    // triángulos entre vecinas nunca se dan vuelta y los polígonos no se cruzan.
    private const double Jitter = 0.25;

    public double Width { get; }
    public double Height { get; }

    /// <summary>Distancia aproximada entre centros de celdas vecinas, en píxeles.</summary>
    public double Spacing { get; }

    public int Count { get; }

    /// <summary>Centro de cada celda.</summary>
    public double[] X { get; }
    public double[] Y { get; }

    /// <summary>
    /// Vecinas de cada celda en orden circular (este, abajo a la derecha, abajo a la izquierda,
    /// oeste, arriba a la izquierda, arriba a la derecha). Las celdas del borde tienen menos.
    /// </summary>
    public int[][] Neighbors { get; }

    /// <summary>
    /// True si la celda está en el borde de la grilla (le faltan vecinas). La grilla se extiende
    /// una celda más allá del mapa, así que estas celdas quedan fuera de la parte visible.
    /// </summary>
    public bool[] IsBorder { get; }

    /// <summary>
    /// Vértices del polígono de cada celda interior (6 puntos), o null en el borde.
    /// El lado compartido con Neighbors[i][k] va del vértice (k + 5) % 6 al vértice k.
    /// </summary>
    public (double X, double Y)[]?[] Polygons { get; }

    public Mesh(double width, double height, double spacing, int seed)
    {
        Width = width;
        Height = height;
        Spacing = spacing;

        var rng = new Rng(seed);
        var rowHeight = spacing * Math.Sqrt(3) / 2; // altura de un triángulo equilátero
        var cols = (int)Math.Ceiling(width / spacing) + 3;
        var rows = (int)Math.Ceiling(height / rowHeight) + 3;

        Count = rows * cols;
        X = new double[Count];
        Y = new double[Count];
        Neighbors = new int[Count][];
        IsBorder = new bool[Count];
        Polygons = new (double, double)[]?[Count];

        // 1 y 2: puntos en la grilla, empezando una celda antes del borde del mapa.
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var i = r * cols + c;
                var shift = r % 2 == 1 ? spacing / 2 : 0;
                X[i] = (c - 1) * spacing + shift + rng.Range(-Jitter, Jitter) * spacing;
                Y[i] = (r - 1) * rowHeight + rng.Range(-Jitter, Jitter) * spacing;
            }
        }

        // 3: vecinas. Las filas impares están corridas a la derecha, por eso cambian las
        // columnas de las vecinas de arriba y de abajo según la paridad de la fila.
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var odd = r % 2 == 1;
                var ring = new (int R, int C)[]
                {
                    (r, c + 1),                       // este
                    (r + 1, odd ? c + 1 : c),         // abajo a la derecha
                    (r + 1, odd ? c : c - 1),         // abajo a la izquierda
                    (r, c - 1),                       // oeste
                    (r - 1, odd ? c : c - 1),         // arriba a la izquierda
                    (r - 1, odd ? c + 1 : c),         // arriba a la derecha
                };

                var list = new List<int>(6);
                foreach (var (nr, nc) in ring)
                {
                    if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                        list.Add(nr * cols + nc);
                }

                var i = r * cols + c;
                Neighbors[i] = list.ToArray();
                IsBorder[i] = list.Count < 6;
            }
        }

        // 4: polígonos de las celdas interiores.
        for (var i = 0; i < Count; i++)
        {
            if (IsBorder[i]) continue;

            var n = Neighbors[i];
            var polygon = new (double X, double Y)[6];
            for (var k = 0; k < 6; k++)
            {
                var a = n[k];
                var b = n[(k + 1) % 6];
                polygon[k] = ((X[i] + X[a] + X[b]) / 3, (Y[i] + Y[a] + Y[b]) / 3);
            }
            Polygons[i] = polygon;
        }
    }

    /// <summary>
    /// Celda que contiene el punto (x, y) en píxeles del mapa, o -1 si no hay ninguna.
    /// Si se conoce una celda cercana (hint), se busca caminando desde ella, que es mucho más
    /// rápido que revisar todas: sirve para el pincel, que pide celdas vecinas una tras otra.
    /// </summary>
    public int FindCell(double x, double y, int hint = -1)
    {
        // 1. El centro más cercano.
        var best = hint >= 0 && Polygons[hint] is not null ? WalkToNearest(hint, x, y) : NearestByScan(x, y);
        if (best < 0) return -1;

        // 2. Los polígonos se arman con baricentros, no con "el centro más cercano", así que
        //    cerca de un borde el punto puede caer en una vecina. Se confirma con la prueba.
        if (Contains(best, x, y)) return best;
        foreach (var n in Neighbors[best])
        {
            if (Polygons[n] is not null && Contains(n, x, y)) return n;
        }
        return best;
    }

    // Revisa todas las celdas. Son unas 10.000 cuentas, nada para una computadora para un clic;
    // con millones de celdas convendría una grilla de búsqueda.
    private int NearestByScan(double x, double y)
    {
        var best = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < Count; i++)
        {
            if (Polygons[i] is null) continue;
            var distance = Distance2(i, x, y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    // Caminata: desde una celda, se pasa a la vecina más cercana al punto mientras haya una más
    // cercana que la actual. En una malla casi regular llega al centro más cercano en pocos pasos.
    private int WalkToNearest(int start, double x, double y)
    {
        var current = start;
        var currentDistance = Distance2(current, x, y);
        while (true)
        {
            var next = -1;
            foreach (var n in Neighbors[current])
            {
                if (Polygons[n] is null) continue;
                var distance = Distance2(n, x, y);
                if (distance < currentDistance)
                {
                    currentDistance = distance;
                    next = n;
                }
            }
            if (next < 0) return current;
            current = next;
        }
    }

    // Distancia al cuadrado: sin raíz, porque para comparar distancias alcanza con el cuadrado.
    private double Distance2(int cell, double x, double y)
    {
        var dx = X[cell] - x;
        var dy = Y[cell] - y;
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// Prueba del rayo: desde el punto se traza una línea horizontal hacia la derecha y se
    /// cuentan los lados del polígono que cruza. Impar: el punto está adentro. Par: afuera.
    /// </summary>
    private bool Contains(int cell, double x, double y)
    {
        var polygon = Polygons[cell]!;
        var inside = false;
        for (int a = 0, b = polygon.Length - 1; a < polygon.Length; b = a++)
        {
            var (xa, ya) = polygon[a];
            var (xb, yb) = polygon[b];

            // ¿El lado cruza la altura del punto, y el cruce queda a la derecha del punto?
            if ((ya > y) != (yb > y) && x < (xb - xa) * (y - ya) / (yb - ya) + xa)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>
    /// Búsqueda en anchura (BFS) desde varias celdas a la vez: devuelve cuántos pasos de vecina
    /// en vecina hay desde la fuente más cercana hasta cada celda. Solo avanza por las celdas
    /// donde canEnter da true; las inalcanzables quedan en int.MaxValue.
    /// </summary>
    public int[] Distances(IEnumerable<int> sources, Func<int, bool> canEnter)
    {
        var distance = new int[Count];
        Array.Fill(distance, int.MaxValue);

        var queue = new Queue<int>();
        foreach (var s in sources)
        {
            distance[s] = 0;
            queue.Enqueue(s);
        }

        // Una cola procesa primero lo que entró primero: así se recorre por "anillos"
        // y cada celda recibe la distancia más corta la primera vez que se la visita.
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in Neighbors[current])
            {
                if (distance[next] != int.MaxValue || !canEnter(next)) continue;
                distance[next] = distance[current] + 1;
                queue.Enqueue(next);
            }
        }

        return distance;
    }
}

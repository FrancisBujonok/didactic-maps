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

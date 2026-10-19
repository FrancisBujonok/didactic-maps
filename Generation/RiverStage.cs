namespace MapaMundos.Generation;

/// <summary>
/// Etapa 3, ríos: hacia dónde drena cada celda y cuánta agua junta.
///
/// 1. Drenaje ("priority flood"): se inunda el mapa desde el mar hacia arriba, siempre por la
///    celda más baja que toca el agua. Cada celda drena hacia la celda desde la que la alcanzó
///    la inundación. Así toda la tierra termina en el mar, incluso si hay pozos sin salida
///    (el agua "llena" el pozo y desborda, como un lago).
/// 2. Caudal: cada celda recibe lluvia según su humedad. Recorriendo de las cumbres hacia el
///    mar, cada celda le pasa su agua a la de abajo. Donde se junta mucha agua hay un río.
/// </summary>
public static class RiverStage
{
    // Un río aparece cuando junta la lluvia de esta fracción de toda la tierra.
    private const double CatchmentFraction = 0.006;

    public static void Apply(World world, int seed)
    {
        var mesh = world.Mesh;
        var n = mesh.Count;

        // ---- 1. Drenaje
        var reached = new bool[n];
        var floodLevel = new double[n];
        var order = new List<int>(n); // tierra y lagos, en el orden en que los alcanzó el agua
        var queue = new PriorityQueue<int, double>();

        foreach (var i in Enumerable.Range(0, n).Where(world.IsOcean))
        {
            reached[i] = true;
            floodLevel[i] = world.Elevation[i];
            queue.Enqueue(i, floodLevel[i]);
        }

        // La cola de prioridad siempre devuelve la celda con menor nivel: el agua sube de a poco.
        while (queue.TryDequeue(out var current, out _))
        {
            foreach (var next in mesh.Neighbors[current])
            {
                if (reached[next]) continue;
                reached[next] = true;

                // Nunca más bajo que la celda de la que viene: así los pozos se rellenan y el
                // camino hacia el mar siempre baja (aunque sea un poquito).
                floodLevel[next] = Math.Max(world.Elevation[next], floodLevel[current] + 1e-5);
                world.Downstream[next] = current;
                order.Add(next);
                queue.Enqueue(next, floodLevel[next]);
            }
        }

        // ---- 2. Lluvia y caudal
        var rainNoise = new PerlinNoise(seed);
        double totalRain = 0;
        var landCells = 0;

        foreach (var i in order)
        {
            var nx = mesh.X[i] / mesh.Height * 3;
            var ny = mesh.Y[i] / mesh.Height * 3;
            var rain = world.Moisture[i] * (1 + rainNoise.Fractal(nx, ny, 3));
            world.Flux[i] = Math.Max(rain, 0);
            totalRain += world.Flux[i];
            landCells++;
        }

        // Al revés del orden de inundación: primero las cumbres. Cuando se procesa una celda,
        // todas las que drenan hacia ella ya le pasaron su agua.
        for (var k = order.Count - 1; k >= 0; k--)
        {
            var cell = order[k];
            var below = world.Downstream[cell];
            if (!world.IsOcean(below))
                world.Flux[below] += world.Flux[cell];
        }

        var meanRain = landCells > 0 ? totalRain / landCells : 0;
        world.RiverThreshold = meanRain * landCells * CatchmentFraction;
    }
}

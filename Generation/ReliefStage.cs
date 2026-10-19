namespace MapaMundos.Generation;

/// <summary>
/// Etapa 1, continente: decide la altura de cada celda y qué es tierra, mar o lago.
///
/// Idea: altura = ruido fractal - caída hacia los bordes. El ruido da costas irregulares,
/// montañas e islas; la caída asegura que el agua rodee la tierra y no se corte en el borde.
/// Después se elige el nivel del mar para que la tierra ocupe el porcentaje pedido.
/// </summary>
public static class ReliefStage
{
    // Cuánto pesa la caída frente al ruido. Más alto: un continente compacto y central.
    // Más bajo: archipiélagos que llegan cerca de los bordes.
    private const double FalloffWeight = 0.55;
    private const double EdgeDrop = 3;

    public static void Apply(World world, int seed, double landRatio)
    {
        var mesh = world.Mesh;
        var noise = new PerlinNoise(seed);

        // Con la misma semilla, pero en otro generador, se eligen la posición del centro del
        // continente y el tamaño de sus rasgos. Así no todos los mundos quedan centrados.
        var rng = new Rng(seed + 1);
        var centerX = 0.5 + rng.Range(-0.08, 0.08);
        var centerY = 0.5 + rng.Range(-0.08, 0.08);
        var frequency = rng.Range(2.0, 3.2);

        var raw = new double[mesh.Count];
        for (var i = 0; i < mesh.Count; i++)
        {
            if (mesh.IsBorder[i])
            {
                raw[i] = double.MinValue; // el borde de la grilla siempre es mar
                continue;
            }

            // Coordenadas del ruido divididas por el alto: los rasgos no se estiran
            // aunque el mapa sea más ancho que alto.
            var nx = mesh.X[i] / mesh.Height * frequency;
            var ny = mesh.Y[i] / mesh.Height * frequency;

            // Distancia al centro, de 0 (centro) a 1 (mitad de un borde).
            var dx = (mesh.X[i] / mesh.Width - centerX) * 2;
            var dy = (mesh.Y[i] / mesh.Height - centerY) * 2;

            // Caída suave hacia afuera, más una caída brusca solo muy cerca de los bordes
            // (la potencia 8 vale casi 0 hasta 0.7 y crece rápido después): así la tierra
            // nunca queda cortada por el borde del mapa.
            var edge = Math.Pow(Math.Max(Math.Abs(dx), Math.Abs(dy)), 8);
            raw[i] = noise.Fractal(nx, ny, 5) - FalloffWeight * (dx * dx + dy * dy) - EdgeDrop * edge;
        }

        // Nivel del mar: si se ordenan las alturas, el valor que deja abajo al (1 - landRatio)
        // de las celdas. Por eso el porcentaje de tierra se cumple siempre, sea cual sea el ruido.
        var interior = raw.Where(v => v > double.MinValue).OrderBy(v => v).ToArray();
        var seaLevel = interior[(int)((1 - landRatio) * (interior.Length - 1))];
        var lowest = interior[0];
        var highest = interior[^1];

        // Se reescala para que el nivel del mar sea 0, la cima 1 y el fondo -1.
        for (var i = 0; i < mesh.Count; i++)
        {
            var v = Math.Max(raw[i], lowest);
            world.Elevation[i] = v > seaLevel
                ? (v - seaLevel) / (highest - seaLevel)
                : (v - seaLevel) / (seaLevel - lowest);
        }

        FindWater(world);
    }

    /// <summary>
    /// Decide qué celdas son agua, cuáles de ellas son lagos y a qué distancia de la costa
    /// está cada celda. Es público porque, si el autor cambia alturas a mano, hay que volver
    /// a calcularlo con las alturas nuevas.
    /// </summary>
    public static void FindWater(World world)
    {
        var mesh = world.Mesh;

        // El borde de la grilla es siempre mar, aunque una edición lo haya levantado.
        for (var i = 0; i < mesh.Count; i++)
            world.Water[i] = mesh.IsBorder[i] || world.Elevation[i] <= 0;

        // Lagos: el agua que no se puede alcanzar desde el borde del mapa nadando
        // de celda de agua en celda de agua está encerrada por tierra.
        var borders = Enumerable.Range(0, mesh.Count).Where(i => mesh.IsBorder[i]);
        var fromOpenSea = mesh.Distances(borders, i => world.Water[i]);
        for (var i = 0; i < mesh.Count; i++)
            world.IsLake[i] = world.Water[i] && fromOpenSea[i] == int.MaxValue;

        // Distancia a la costa, para tierra y agua: se parte de las celdas que tienen
        // al menos una vecina del otro tipo y se avanza sin cruzar la costa.
        var coast = Enumerable.Range(0, mesh.Count)
            .Where(i => mesh.Neighbors[i].Any(n => world.Water[n] != world.Water[i]));
        var cells = mesh.Count;
        var distance = new int[cells];
        var land = mesh.Distances(coast.Where(world.IsLand), world.IsLand);
        var water = mesh.Distances(coast.Where(i => world.Water[i]), i => world.Water[i]);
        for (var i = 0; i < cells; i++)
            distance[i] = world.Water[i] ? water[i] : land[i];
        world.CoastDistance = distance;
    }
}

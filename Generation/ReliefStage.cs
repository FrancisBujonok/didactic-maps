using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>
/// Etapa 1, continente: decide la altura de cada celda y qué es tierra, mar o lago.
///
/// Idea: altura = ruido fractal + la forma de la plantilla (ver ReliefShape). El ruido da
/// costas irregulares, montañas e islas; la forma decide dónde va la tierra (un continente,
/// un archipiélago, un atolón...). Después se elige el nivel del mar para que la tierra ocupe
/// el porcentaje pedido.
/// </summary>
public static class ReliefStage
{
    public static void Apply(World world, GeneratorSettings settings)
    {
        var mesh = world.Mesh;
        var shape = ReliefShape.Create(settings.Template, settings.ContinentSeed, mesh.Width, mesh.Height,
            settings.Sketch, settings.SketchFidelity, settings.SketchFeatures);

        var points = Enumerable.Range(0, mesh.Count).Select(i => (mesh.X[i], mesh.Y[i])).ToArray();
        var elevations = Elevations(shape, points, mesh.IsBorder, settings.LandRatio);
        Array.Copy(elevations, world.Elevation, mesh.Count);

        FindWater(world);
    }

    /// <summary>
    /// Alturas finales (-1 a 1, mar en 0) de una lista de puntos en píxeles del mapa. La usan
    /// esta etapa (centros de celda) y las miniaturas (píxeles), así se ven iguales. Va en dos
    /// pasos para que los retoques dibujados no muevan el resto del mapa:
    /// 1. Relieve de la plantilla y su nivel del mar (por cuantil, como siempre).
    /// 2. Retoques encima, medidos desde ese nivel del mar: una isla agregada no le "roba" tierra
    ///    a la costa de otro lado, y un agua dibujada no hace crecer la tierra en otra parte.
    /// Sin retoques, el paso 2 no cambia nada y el mapa sale igual que antes.
    /// </summary>
    public static double[] Elevations(ReliefShape shape, IReadOnlyList<(double X, double Y)> points,
        IReadOnlyList<bool>? border, double landRatio)
    {
        var raw = new double[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            raw[i] = border is not null && border[i]
                ? double.MinValue // el borde de la grilla siempre es mar
                : shape.Height(points[i].X, points[i].Y);
        }

        var (seaLevel, lowest, highest) = Levels(raw.Where(v => v > double.MinValue), shape, landRatio);

        for (var i = 0; i < points.Count; i++)
        {
            if (raw[i] > double.MinValue)
                raw[i] = shape.Overlay(points[i].X, points[i].Y, raw[i], seaLevel, highest - seaLevel);
        }

        // Se reescala para que el nivel del mar sea 0, la cima 1 y el fondo -1, con la cima y el
        // fondo de la plantilla sola. Si un retoque sube más (una cordillera dibujada) se recorta
        // en 1: con la cima nueva, todas las montañas del resto del mapa bajarían.
        var result = new double[points.Count];
        for (var i = 0; i < points.Count; i++)
            result[i] = Math.Clamp(Scale(Math.Max(raw[i], lowest), seaLevel, lowest, highest, shape.LandBase), -1, 1);
        return result;
    }

    /// <summary>
    /// Nivel del mar, altura mínima y máxima. Normalmente el nivel del mar es el valor que, si
    /// se ordenan las alturas, deja abajo al (1 - landRatio) de ellas: así el porcentaje de tierra
    /// se cumple siempre, sea cual sea el ruido. Con el dibujo es fijo (0): la tierra es la que
    /// dibujó el autor. Lo usan esta etapa y las miniaturas, para que coincidan.
    /// </summary>
    public static (double SeaLevel, double Lowest, double Highest) Levels(IEnumerable<double> heights, ReliefShape shape, double landRatio)
    {
        var sorted = heights.OrderBy(v => v).ToArray();
        var seaLevel = shape.FixedSeaLevel ? 0 : sorted[(int)((1 - landRatio) * (sorted.Length - 1))];

        // Con un dibujo vacío o diminuto puede no haber tierra (o no haber mar): se corre el
        // extremo un poco para no dividir por cero al reescalar.
        var lowest = Math.Min(sorted[0], seaLevel - 1e-6);
        var highest = Math.Max(sorted[^1], seaLevel + 1e-6);
        return (seaLevel, lowest, highest);
    }

    /// <summary>
    /// Reescala una altura cruda: nivel del mar 0, cima 1, fondo -1. Con landBase (el dibujo),
    /// la tierra se mide desde esa altura: lo que queda por debajo es llanura baja (0.01, apenas
    /// sobre el mar para seguir siendo tierra) y solo lo que sube por encima gana altura.
    /// </summary>
    public static double Scale(double value, double seaLevel, double lowest, double highest, double landBase = 0)
    {
        if (value <= seaLevel) return (value - seaLevel) / (seaLevel - lowest);
        if (landBase <= 0) return (value - seaLevel) / (highest - seaLevel);

        var above = (value - seaLevel - landBase) / (highest - seaLevel - landBase);
        return 0.01 + 0.99 * Math.Clamp(above, 0, 1);
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

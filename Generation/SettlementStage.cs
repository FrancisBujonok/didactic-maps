using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>Un lugar a crear: en qué celda, de qué tipo, de qué tamaño y con qué nombre.</summary>
public sealed record PlannedPlace(int Cell, string Kind, PinSize Size, string Name);

/// <summary>Un reino a crear: nombre, celdas y cuál de los lugares planeados es su capital.</summary>
public sealed record PlannedKingdom(string Name, List<int> Cells, int Capital);

/// <summary>Resultado de la etapa: los lugares y, si se pidieron, los reinos nuevos.</summary>
public sealed class SettlementPlan
{
    public List<PlannedPlace> Places { get; } = new();
    public List<PlannedKingdom> Kingdoms { get; } = new();

    /// <summary>Para reinos que ya existían: índice del reino -> índice del lugar que es su capital.</summary>
    public Dictionary<int, int> CapitalsOfExisting { get; } = new();
}

/// <summary>
/// Etapa de poblados: decide dónde se fundan ciudades, aldeas y otros lugares, y opcionalmente
/// reparte el continente en reinos alrededor de sus capitales.
///
/// 1. Puntaje: cada celda de tierra recibe un puntaje de "qué tan buen lugar es para vivir":
///    agua dulce (ríos, lagos), costa, tierra fértil y poca altura.
/// 2. Ciudades y aldeas: se toma la mejor celda, se descartan sus alrededores, y se repite. La
///    semilla suma una pequeña variación al puntaje: otra semilla elige otros buenos lugares.
/// 3. Reinos: capitales bien separadas entre las ciudades; cada reino crece desde su capital por
///    el camino más barato, y cruzar montañas o ríos cuesta más. Así las fronteras tienden a
///    seguir cordilleras y ríos, como en la historia real.
/// 4. Lugares con lógica: fortalezas en las fronteras, cuevas al pie de las montañas, ruinas en
///    zonas apartadas y bosques sagrados en lo profundo de los bosques.
/// </summary>
public static class SettlementStage
{
    // Separación mínima entre ciudades según la densidad (pocos, normal, muchos), en píxeles del
    // mapa (2000 x 1250). Las aldeas se separan menos: llenan los huecos entre ciudades.
    private static readonly double[] CityGap = { 230, 175, 135 };
    private static readonly double[] VillageGap = { 120, 90, 70 };

    // Fertilidad de cada bioma: cuánta gente puede alimentar.
    private static readonly Dictionary<Biome, double> Fertility = new()
    {
        [Biome.Grassland] = 1.0,
        [Biome.TemperateForest] = 0.8,
        [Biome.Savanna] = 0.7,
        [Biome.TemperateRainforest] = 0.6,
        [Biome.TropicalForest] = 0.6,
        [Biome.Taiga] = 0.45,
        [Biome.Jungle] = 0.4,
        [Biome.Tundra] = 0.2,
        [Biome.Desert] = 0.15,
        [Biome.Mountain] = 0.1,
    };

    private static readonly Biome[] Forests =
    {
        Biome.TemperateForest, Biome.TemperateRainforest, Biome.TropicalForest, Biome.Jungle, Biome.Taiga
    };

    /// <param name="existingRegions">
    /// Reinos que ya tiene el proyecto. Si no se piden reinos nuevos (kingdomCount = 0), cada uno
    /// recibe como capital su mejor ciudad.
    /// </param>
    public static SettlementPlan Plan(World world, int seed, int density, int kingdomCount, IReadOnlyList<Region> existingRegions)
    {
        var mesh = world.Mesh;
        var rng = new Rng(seed);
        var names = new NameGenerator(seed + 1);
        var plan = new SettlementPlan();
        density = Math.Clamp(density, 0, 2);

        // ---- 1. Puntaje de cada celda (con la variación de la semilla).
        var score = new double[mesh.Count];
        for (var i = 0; i < mesh.Count; i++)
            score[i] = IsHabitable(world, i) ? Suitability(world, i) + rng.Range(0, 0.35) : double.MinValue;

        var byScore = Enumerable.Range(0, mesh.Count)
            .Where(i => score[i] > double.MinValue)
            .OrderByDescending(i => score[i])
            .ToList();

        // ---- 2. Ciudades: las mejores celdas, separadas entre sí.
        var cities = PickSpaced(world, byScore.Where(i => score[i] >= 0.9), CityGap[density], new List<int>(), max: 60);

        // Aldeas: lo que queda bueno, separado de todo lo anterior, una por ciudad.
        var villages = PickSpaced(world, byScore.Where(i => score[i] >= 0.5), VillageGap[density], cities, max: cities.Count);

        // ---- 3. Reinos.
        var capitals = new List<int>();
        int[] owner;
        if (kingdomCount > 0 && cities.Count > 0)
        {
            capitals = PickCapitals(world, cities, Math.Min(kingdomCount, cities.Count));
            owner = GrowKingdoms(world, capitals);
        }
        else
        {
            owner = ThematicRenderer.Owners(world, existingRegions);
        }

        // Los lugares se agregan en orden; se guarda el índice de cada ciudad para las capitales.
        var placeOfCell = new Dictionary<int, int>();
        void Add(int cell, string kind, PinSize size)
        {
            placeOfCell[cell] = plan.Places.Count;
            plan.Places.Add(new PlannedPlace(cell, kind, size, names.Place()));
        }

        foreach (var city in cities)
        {
            if (capitals.Contains(city))
                Add(city, "Ciudad", PinSize.Large);
            else if (TouchesOcean(world, city))
                Add(city, "Puerto", PinSize.Medium);
            else
                Add(city, "Ciudad", PinSize.Medium);
        }

        foreach (var village in villages)
            Add(village, rng.NextDouble() < 0.15 ? "Posada" : "Aldea", PinSize.Small);

        if (capitals.Count > 0)
        {
            foreach (var capital in capitals)
            {
                var index = capitals.IndexOf(capital);
                var cells = Enumerable.Range(0, mesh.Count).Where(i => owner[i] == index).ToList();
                plan.Kingdoms.Add(new PlannedKingdom(names.Kingdom(), cells, placeOfCell[capital]));
            }
        }
        else
        {
            // Reinos existentes: la mejor ciudad de cada uno es su capital. Si un reino no tiene
            // ninguna, se funda una en su mejor celda.
            for (var r = 0; r < existingRegions.Count; r++)
            {
                var inside = cities.Where(c => owner[c] == r).OrderByDescending(c => score[c]).ToList();
                var cell = inside.Count > 0 ? inside[0] : byScore.FirstOrDefault(c => owner[c] == r, -1);
                if (cell < 0) continue;

                if (!placeOfCell.ContainsKey(cell)) Add(cell, "Ciudad", PinSize.Large);
                var place = placeOfCell[cell];
                plan.Places[place] = plan.Places[place] with { Kind = "Ciudad", Size = PinSize.Large };
                plan.CapitalsOfExisting[r] = place;
            }
        }

        // ---- 4. Lugares con lógica. Todos lejos de los poblados y separados entre sí.
        var settled = plan.Places.Select(p => p.Cell).ToList();
        var extras = density + 2; // de cada tipo: 2, 3 o 4

        // Fortalezas: sobre fronteras entre dos reinos.
        var frontier = byScore.Where(i => owner[i] >= 0 && mesh.Neighbors[i].Any(n => owner[n] >= 0 && owner[n] != owner[i]));
        foreach (var cell in PickSpaced(world, Shuffle(frontier, rng), 150, settled, max: extras * 2, takenGap: 50))
            Add(cell, "Fortaleza", PinSize.Medium);

        // Cuevas: montañas que tocan tierra baja (al pie de la cordillera).
        var foothills = Enumerable.Range(0, mesh.Count).Where(i =>
            world.IsLand(i) && world.Biomes[i] == Biome.Mountain &&
            mesh.Neighbors[i].Any(n => world.IsLand(n) && world.Biomes[n] != Biome.Mountain && world.Biomes[n] != Biome.Snow));
        foreach (var cell in PickSpaced(world, Shuffle(foothills, rng), 160, plan.Places.Select(p => p.Cell).ToList(), max: extras, takenGap: 60))
            Add(cell, "Cueva", PinSize.Small);

        // Ruinas y bosques sagrados: las celdas más alejadas de cualquier poblado.
        var remote = FarthestFrom(world, settled);
        var land = Enumerable.Range(0, mesh.Count).Where(i => world.IsLand(i) && !mesh.IsBorder[i]);
        var ruins = land.Where(i => world.Biomes[i] != Biome.Snow && world.Biomes[i] != Biome.Ice)
                        .OrderByDescending(i => remote[i] + rng.Range(0, 3));
        foreach (var cell in PickSpaced(world, ruins, 170, plan.Places.Select(p => p.Cell).ToList(), max: extras, takenGap: 80))
            Add(cell, "Ruina", PinSize.Small);

        var groves = land.Where(i => Forests.Contains(world.Biomes[i]))
                         .OrderByDescending(i => remote[i] + rng.Range(0, 3));
        foreach (var cell in PickSpaced(world, groves, 170, plan.Places.Select(p => p.Cell).ToList(), max: Math.Max(1, extras - 1), takenGap: 80))
            Add(cell, "Bosque sagrado", PinSize.Small);

        return plan;
    }

    // ------------------------------------------------------------------ puntaje

    private static bool IsHabitable(World world, int cell) =>
        world.IsLand(cell) && !world.Mesh.IsBorder[cell] &&
        world.Biomes[cell] is not (Biome.Ice or Biome.Snow);

    /// <summary>Qué tan buen lugar es la celda para fundar un poblado (más alto, mejor).</summary>
    private static double Suitability(World world, int cell)
    {
        var mesh = world.Mesh;
        var value = Fertility.GetValueOrDefault(world.Biomes[cell], 0.3);

        // Agua dulce: lo más importante para una ciudad antigua. Cuanto más grande el río, mejor.
        value += world.RiverSize(cell) switch { 0 => 0.5, 1 => 0.8, 2 => 1.0, _ => 0 };
        if (mesh.Neighbors[cell].Any(n => world.IsLake[n])) value += 0.4;

        // Costa: comercio y pesca.
        if (TouchesOcean(world, cell)) value += 0.6;

        // La altura complica todo: cultivar, construir, llegar.
        value -= world.Elevation[cell] * 0.6;

        return value;
    }

    private static bool TouchesOcean(World world, int cell) =>
        world.Mesh.Neighbors[cell].Any(world.IsOcean);

    // ---------------------------------------------------------------- elección

    /// <summary>
    /// Recorre los candidatos en orden y se queda con los que están a más de "gap" píxeles de
    /// los ya elegidos y a más de "takenGap" de los de "taken" (lo que ya había en el mapa). Es
    /// la manera simple de repartir puntos sin que se amontonen. Por defecto takenGap = gap; los
    /// lugares extra usan uno menor para caber entre los poblados.
    /// </summary>
    private static List<int> PickSpaced(World world, IEnumerable<int> candidates, double gap,
        IReadOnlyCollection<int> taken, int max, double? takenGap = null)
    {
        var mesh = world.Mesh;
        var chosen = new List<int>();
        var gap2 = gap * gap; // se comparan distancias al cuadrado: no hace falta la raíz
        var takenGap2 = (takenGap ?? gap) * (takenGap ?? gap);

        foreach (var cell in candidates)
        {
            if (chosen.Count >= max) break;

            bool Within(int other, double limit2)
            {
                var dx = mesh.X[other] - mesh.X[cell];
                var dy = mesh.Y[other] - mesh.Y[cell];
                return dx * dx + dy * dy < limit2;
            }

            if (chosen.Any(c => Within(c, gap2)) || taken.Any(t => Within(t, takenGap2))) continue;
            chosen.Add(cell);
        }
        return chosen;
    }

    /// <summary>
    /// Capitales: la mejor ciudad primero; después, una y otra vez, la ciudad más alejada de las
    /// capitales ya elegidas ("el punto más lejano"). Así los reinos quedan repartidos y no todos
    /// amontonados en la zona más fértil.
    /// </summary>
    private static List<int> PickCapitals(World world, List<int> cities, int count)
    {
        var mesh = world.Mesh;
        var capitals = new List<int> { cities[0] };

        while (capitals.Count < count)
        {
            var next = cities
                .Where(c => !capitals.Contains(c))
                .OrderByDescending(c => capitals.Min(k =>
                    (mesh.X[k] - mesh.X[c]) * (mesh.X[k] - mesh.X[c]) + (mesh.Y[k] - mesh.Y[c]) * (mesh.Y[k] - mesh.Y[c])))
                .First();
            capitals.Add(next);
        }
        return capitals;
    }

    /// <summary>
    /// Reparte la tierra entre los reinos: cada celda es del reino cuya capital llega a ella por
    /// el camino más barato (algoritmo de Dijkstra con varias fuentes). Avanzar por llanura
    /// cuesta 1; subir a montaña o cruzar un río grande cuesta más; el mar no se cruza. Devuelve
    /// el reino de cada celda (índice en capitals) o -1.
    /// </summary>
    private static int[] GrowKingdoms(World world, List<int> capitals)
    {
        var mesh = world.Mesh;
        var owner = new int[mesh.Count];
        var cost = new double[mesh.Count];
        Array.Fill(owner, -1);
        Array.Fill(cost, double.MaxValue);

        var queue = new PriorityQueue<int, double>();
        for (var k = 0; k < capitals.Count; k++)
        {
            owner[capitals[k]] = k;
            cost[capitals[k]] = 0;
            queue.Enqueue(capitals[k], 0);
        }

        while (queue.TryDequeue(out var cell, out var reached))
        {
            if (reached > cost[cell]) continue; // ya se llegó a esta celda por un camino más barato

            foreach (var next in mesh.Neighbors[cell])
            {
                if (!world.IsLand(next) || mesh.IsBorder[next]) continue;

                var step = 1.0;
                if (world.Biomes[next] is Biome.Mountain or Biome.Snow) step += 5;   // cordilleras
                if (world.RiverSize(next) >= 1) step += 3;                           // ríos anchos

                var total = cost[cell] + step;
                if (total < cost[next])
                {
                    cost[next] = total;
                    owner[next] = owner[cell];
                    queue.Enqueue(next, total);
                }
            }
        }
        return owner;
    }

    /// <summary>Distancia en celdas desde cada celda de tierra hasta el poblado más cercano.</summary>
    private static int[] FarthestFrom(World world, List<int> settled) =>
        world.Mesh.Distances(settled, world.IsLand);

    /// <summary>Mezcla de Fisher-Yates (la misma que usa PerlinNoise), con la semilla de la etapa.</summary>
    private static List<int> Shuffle(IEnumerable<int> items, Rng rng)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.NextInt(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}

using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>Un camino planeado: entre dos lugares (índices en la lista que se pasó a Plan).</summary>
public sealed record PlannedRoute(int From, int To, RouteKind Kind, List<int> Cells);

/// <summary>
/// Caminos entre lugares, por el terreno más fácil.
///
/// La malla de celdas es un grafo: cada celda es un nodo y cada vecina, una arista. Ir a una
/// celda vecina cuesta su distancia multiplicada por lo difícil del terreno (montaña, bosque),
/// más un recargo por subir y otro por cruzar un río ancho; el mar no se cruza. El camino más
/// barato entre dos celdas sale con A*, que es Dijkstra con una pista: además de lo recorrido,
/// mira cuánto falta en línea recta, y así explora primero hacia el destino.
///
/// Para unir muchos lugares no se trazan todos con todos: se arma un árbol de expansión mínima
/// (Kruskal), el conjunto de caminos más barato que conecta a todos, y se suman unos pocos
/// atajos para que haya circuitos. Al trazarlos, los tramos ya hechos cuestan la mitad: los
/// caminos siguientes los aprovechan y se juntan en troncales, como los caminos reales.
/// </summary>
public static class RouteStage
{
    // Un tramo por un camino ya trazado cuesta esto respecto del terreno normal.
    private const double RoadDiscount = 0.5;

    // Cada lugar principal considera unir con sus vecinos más cercanos, hasta esta distancia.
    private const int CandidatesPerPlace = 4;
    private const double MaxCandidateDistance = 700; // px del mapa

    /// <summary>Cuánto cuesta atravesar una celda de cada bioma, respecto de la llanura (1).</summary>
    public static double TerrainFactor(Biome biome) => biome switch
    {
        Biome.Snow => 6,
        Biome.Mountain => 4,
        Biome.Jungle => 2,
        Biome.TemperateRainforest or Biome.TropicalForest or Biome.Taiga or Biome.TemperateForest => 1.5,
        Biome.Desert => 1.3,
        Biome.Tundra or Biome.Ice => 1.2,
        _ => 1
    };

    /// <summary>Costo de pasar de una celda a su vecina (en "celdas de llanura").</summary>
    private static double StepCost(World world, int from, int to, ISet<int>? roads)
    {
        var mesh = world.Mesh;
        var distance = Math.Sqrt(Math.Pow(mesh.X[to] - mesh.X[from], 2) + Math.Pow(mesh.Y[to] - mesh.Y[from], 2)) / mesh.Spacing;
        var climb = Math.Max(0, world.Elevation[to] - world.Elevation[from]) * 8; // subir cansa; bajar no tanto
        var river = world.RiverSize(to) >= 1 ? 2 : 0;                               // cruzar un río ancho: hace falta un vado o un puente
        var cost = distance * TerrainFactor(world.Biomes[to]) + climb + river;
        return roads is not null && roads.Contains(to) ? cost * RoadDiscount : cost;
    }

    private static bool Walkable(World world, int cell) => world.IsLand(cell) && !world.Mesh.IsBorder[cell];

    /// <summary>
    /// El camino más barato de una celda a otra por tierra (A*), o null si no hay (por ejemplo,
    /// si están en islas distintas). Con roads, los tramos por esas celdas cuestan la mitad.
    /// </summary>
    public static List<int>? FindPath(World world, int start, int goal, ISet<int>? roads = null)
    {
        if (!Walkable(world, start) || !Walkable(world, goal)) return null;

        var mesh = world.Mesh;
        // La pista de A* nunca puede pasarse de lo que de verdad falta: en línea recta, y por el
        // terreno más barato posible (un camino ya trazado, si los hay).
        var cheapest = roads is null ? 1 : RoadDiscount;
        double Hint(int cell) => Math.Sqrt(Math.Pow(mesh.X[goal] - mesh.X[cell], 2) + Math.Pow(mesh.Y[goal] - mesh.Y[cell], 2)) / mesh.Spacing * cheapest;

        var cost = new Dictionary<int, double> { [start] = 0 };
        var cameFrom = new Dictionary<int, int>();
        var queue = new PriorityQueue<int, double>();
        queue.Enqueue(start, Hint(start));

        while (queue.TryDequeue(out var cell, out _))
        {
            if (cell == goal)
            {
                var path = new List<int> { goal };
                while (cameFrom.TryGetValue(path[^1], out var previous)) path.Add(previous);
                path.Reverse();
                return path;
            }

            foreach (var next in mesh.Neighbors[cell])
            {
                if (!Walkable(world, next)) continue;
                var total = cost[cell] + StepCost(world, cell, next, roads);
                if (cost.TryGetValue(next, out var known) && known <= total) continue;
                cost[next] = total;
                cameFrom[next] = cell;
                queue.Enqueue(next, total + Hint(next));
            }
        }
        return null;
    }

    /// <summary>Costo total de un camino ya encontrado (sin descuento de caminos).</summary>
    public static double PathCost(World world, IReadOnlyList<int> path)
    {
        var total = 0.0;
        for (var i = 1; i < path.Count; i++) total += StepCost(world, path[i - 1], path[i], null);
        return total;
    }

    /// <summary>
    /// Planea la red de caminos. places son celdas: los principales (ciudades, puertos) forman la
    /// red con caminos; los menores (aldeas, posadas, fortalezas) se unen con un sendero al
    /// punto más cercano de la red. Los índices de PlannedRoute son posiciones en places.
    /// </summary>
    public static List<PlannedRoute> Plan(World world, IReadOnlyList<int> places, IReadOnlyList<bool> major)
    {
        var mesh = world.Mesh;
        var majors = Enumerable.Range(0, places.Count).Where(i => major[i] && Walkable(world, places[i])).ToList();
        double Distance(int a, int b) =>
            Math.Sqrt(Math.Pow(mesh.X[places[a]] - mesh.X[places[b]], 2) + Math.Pow(mesh.Y[places[a]] - mesh.Y[places[b]], 2));

        // 1. Candidatos: cada lugar principal con sus vecinos más cercanos, con su costo real.
        var candidates = new Dictionary<(int, int), double>();
        foreach (var a in majors)
        {
            var nearest = majors.Where(b => b != a && Distance(a, b) <= MaxCandidateDistance)
                .OrderBy(b => Distance(a, b)).Take(CandidatesPerPlace);
            foreach (var b in nearest)
            {
                var key = a < b ? (a, b) : (b, a);
                if (candidates.ContainsKey(key)) continue;
                if (FindPath(world, places[key.Item1], places[key.Item2]) is { } path)
                    candidates[key] = PathCost(world, path);
            }
        }

        // 2. Árbol de expansión mínima (Kruskal): de la unión más barata a la más cara, se acepta
        //    cada una que junte dos grupos todavía separados. "Union-find" lleva los grupos.
        var group = Enumerable.Range(0, places.Count).ToArray();
        int Find(int x) { while (group[x] != x) x = group[x] = group[group[x]]; return x; }

        var chosen = new List<(int A, int B, double Cost)>();
        var rest = new List<(int A, int B, double Cost)>();
        foreach (var ((a, b), cost) in candidates.OrderBy(c => c.Value))
        {
            var (ga, gb) = (Find(a), Find(b));
            if (ga != gb) { group[ga] = gb; chosen.Add((a, b, cost)); }
            else rest.Add((a, b, cost));
        }

        // 3. Atajos: una unión que el árbol deja muy larga (el viaje por la red cuesta más del
        //    doble que el directo) se agrega igual. Pocos, para que no se llene de caminos.
        foreach (var (a, b, cost) in rest)
        {
            if (chosen.Count >= majors.Count - 1 + Math.Max(1, majors.Count / 3)) break;
            if (cost * 2 < NetworkCost(places.Count, chosen, a, b)) chosen.Add((a, b, cost));
        }

        // 4. Recorridos: de la unión más barata a la más cara, aprovechando los tramos ya hechos.
        var roads = new HashSet<int>();
        var result = new List<PlannedRoute>();
        foreach (var (a, b, _) in chosen.OrderBy(c => c.Cost))
        {
            if (FindPath(world, places[a], places[b], roads) is not { } path) continue;
            roads.UnionWith(path);
            result.Add(new PlannedRoute(a, b, RouteKind.Road, path));
        }

        // 5. Senderos: cada lugar menor, hasta el lugar principal más cercano (con el descuento,
        //    el sendero tiende a desembocar en un camino que ya pasa cerca).
        foreach (var m in Enumerable.Range(0, places.Count).Where(i => !major[i] && Walkable(world, places[i])))
        {
            var target = majors.OrderBy(b => Distance(m, b)).Take(2)
                .Select(b => (B: b, Path: FindPath(world, places[m], places[b], roads)))
                .Where(x => x.Path is not null)
                .OrderBy(x => PathCost(world, x.Path!))
                .FirstOrDefault();
            if (target.Path is null) continue;
            result.Add(new PlannedRoute(m, target.B, RouteKind.Trail, target.Path));
        }

        return result;
    }

    /// <summary>Costo del viaje más barato entre dos lugares usando solo las uniones elegidas (Dijkstra).</summary>
    private static double NetworkCost(int count, List<(int A, int B, double Cost)> edges, int from, int to)
    {
        var cost = Enumerable.Repeat(double.MaxValue, count).ToArray();
        var queue = new PriorityQueue<int, double>();
        cost[from] = 0;
        queue.Enqueue(from, 0);
        while (queue.TryDequeue(out var node, out var reached))
        {
            if (node == to) return reached;
            if (reached > cost[node]) continue;
            foreach (var (a, b, c) in edges)
            {
                var other = a == node ? b : b == node ? a : -1;
                if (other < 0 || cost[node] + c >= cost[other]) continue;
                cost[other] = cost[node] + c;
                queue.Enqueue(other, cost[other]);
            }
        }
        return double.MaxValue;
    }

    /// <summary>
    /// Recorrido para dibujar: de la posición exacta del primer lugar a la del segundo, pasando
    /// por los centros de las celdas, suavizado con Chaikin (como los ríos) y simplificado.
    /// En píxeles del mapa.
    /// </summary>
    public static List<(double X, double Y)> PathPoints(World world, IReadOnlyList<int> cells, (double X, double Y) from, (double X, double Y) to)
    {
        var mesh = world.Mesh;
        var points = new List<(double X, double Y)> { from };
        for (var i = 1; i < cells.Count - 1; i++) points.Add((mesh.X[cells[i]], mesh.Y[cells[i]]));
        points.Add(to);
        return Sketches.Simplify(SvgMapRenderer.SmoothOpen(points), 1.2);
    }
}

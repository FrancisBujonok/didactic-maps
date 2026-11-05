namespace MapaMundos.Tests;

/// <summary>Caminos entre lugares (Generation/RouteStage.cs) y cuentas de viaje (Travel.cs).</summary>
public class RouteTests
{
    private static readonly World Continent = TestWorlds.Build(TestWorlds.Settings());

    // Dos celdas de tierra lejanas entre sí, en la misma masa de tierra.
    private static (int A, int B) FarApartLand(World world)
    {
        var mesh = world.Mesh;
        var a = Enumerable.Range(0, mesh.Count).Where(i => world.IsLand(i) && !mesh.IsBorder[i]).MinBy(i => mesh.X[i]);
        var reach = mesh.Distances(new[] { a }, world.IsLand);
        var b = Enumerable.Range(0, mesh.Count).Where(i => reach[i] != int.MaxValue).MaxBy(i => mesh.X[i]);
        return (a, b);
    }

    [Fact]
    public void FindPath_GoesFromStartToGoal_StepByStep_OnlyOverLand()
    {
        var (a, b) = FarApartLand(Continent);

        var path = RouteStage.FindPath(Continent, a, b);

        Assert.NotNull(path);
        Assert.Equal(a, path![0]);
        Assert.Equal(b, path[^1]);
        Assert.All(path, cell => Assert.True(Continent.IsLand(cell)));
        // Cada paso va a una celda vecina: el camino no salta.
        for (var i = 1; i < path.Count; i++)
            Assert.Contains(path[i], Continent.Mesh.Neighbors[path[i - 1]]);
    }

    [Fact]
    public void FindPath_BetweenSeparateIslands_IsNull()
    {
        var islands = TestWorlds.Build(TestWorlds.Settings(ReliefTemplate.Archipelago, 7));
        var mesh = islands.Mesh;
        var a = Enumerable.Range(0, mesh.Count).First(i => islands.IsLand(i) && !mesh.IsBorder[i]);
        var reach = mesh.Distances(new[] { a }, islands.IsLand);
        var other = Enumerable.Range(0, mesh.Count).First(i => islands.IsLand(i) && !mesh.IsBorder[i] && reach[i] == int.MaxValue);

        Assert.Null(RouteStage.FindPath(islands, a, other));
    }

    [Fact]
    public void FindPath_IsTheCheapest_NotTheShortest()
    {
        // El camino más barato no tiene por qué ser el de menos celdas: rodea montañas. Lo que
        // sí tiene que cumplir es costar menos o lo mismo que el de menos celdas (el que da la
        // búsqueda por anchura de la malla).
        var (a, b) = FarApartLand(Continent);
        var cheapest = RouteStage.FindPath(Continent, a, b)!;
        var shortest = ShortestByCells(Continent, a, b);

        Assert.True(RouteStage.PathCost(Continent, cheapest) <= RouteStage.PathCost(Continent, shortest) + 1e-9);
    }

    [Fact]
    public void Plan_ConnectsEveryCityOfTheContinent()
    {
        var settlements = SettlementStage.Plan(Continent, 5, 1, 0, new List<Region>());
        var places = settlements.Places.Select(p => p.Cell).ToList();
        var major = settlements.Places.Select(p => p.Kind is "Ciudad" or "Puerto").ToList();

        var routes = RouteStage.Plan(Continent, places, major);

        // Desde la primera ciudad, siguiendo caminos, se llega a todas las ciudades de su misma
        // masa de tierra.
        var cities = Enumerable.Range(0, places.Count).Where(i => major[i]).ToList();
        var reach = Continent.Mesh.Distances(new[] { places[cities[0]] }, Continent.IsLand);
        var sameLand = cities.Where(i => reach[places[i]] != int.MaxValue).ToList();

        var connected = new HashSet<int> { cities[0] };
        bool grew;
        do
        {
            grew = false;
            foreach (var r in routes.Where(r => r.Kind == RouteKind.Road))
            {
                if (connected.Contains(r.From) && connected.Add(r.To)) grew = true;
                if (connected.Contains(r.To) && connected.Add(r.From)) grew = true;
            }
        } while (grew);

        Assert.All(sameLand, city => Assert.Contains(city, connected));
        Assert.Contains(routes, r => r.Kind == RouteKind.Trail); // las aldeas reciben su sendero
    }

    [Fact]
    public void Travel_KilometersFollowTheScale_AndDaysTheSpeeds()
    {
        // Un tramo recto de 100 px con 0,6 km por píxel mide 60 km.
        var (a, _) = FarApartLand(Continent);
        var start = (Continent.Mesh.X[a], Continent.Mesh.Y[a]);
        var path = new List<(double X, double Y)> { start, (start.Item1 + 100, start.Item2) };

        var trip = Travel.Estimate(Continent, path, 0.6, RouteKind.Road);

        Assert.Equal(60, trip.Km, precision: 6);
        Assert.True(trip.WalkDays >= 60 / Travel.WalkKmPerDay); // el terreno solo puede frenar
        Assert.Equal(trip.WalkDays / 2, trip.HorseDays, precision: 6); // a caballo, el doble de rápido
    }

    // Camino de menos celdas, con una búsqueda por anchura que recuerda de dónde vino cada celda.
    private static List<int> ShortestByCells(World world, int a, int b)
    {
        var from = new Dictionary<int, int> { [a] = a };
        var queue = new Queue<int>(new[] { a });
        while (queue.TryDequeue(out var cell) && cell != b)
        {
            foreach (var next in world.Mesh.Neighbors[cell])
            {
                if (!world.IsLand(next) || world.Mesh.IsBorder[next] || from.ContainsKey(next)) continue;
                from[next] = cell;
                queue.Enqueue(next);
            }
        }
        var path = new List<int> { b };
        while (path[^1] != a) path.Add(from[path[^1]]);
        path.Reverse();
        return path;
    }
}

using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>Distancia de un recorrido y cuánto se tarda en hacerlo.</summary>
public sealed record TravelEstimate(double Km, double WalkDays, double HorseDays, double CartDays);

/// <summary>
/// Cuentas de viaje: kilómetros y días, para que los viajes de una novela tengan sentido.
/// Las velocidades son las de una jornada de marcha con descansos, aproximadas: a pie unos
/// 25 km por día, a caballo 50 y en carreta 20. El terreno frena (una montaña lleva el doble
/// de tiempo) y un camino acelera respecto de ir campo traviesa.
/// </summary>
public static class Travel
{
    public const double WalkKmPerDay = 25;
    public const double HorseKmPerDay = 50;
    public const double CartKmPerDay = 20;

    /// <summary>Cuánto más tarda cruzar cada bioma que una llanura.</summary>
    public static double TerrainSlowdown(Biome biome) => biome switch
    {
        Biome.Snow => 2.5,
        Biome.Mountain => 2,
        Biome.Jungle => 1.6,
        Biome.TemperateRainforest or Biome.TropicalForest or Biome.Taiga or Biome.TemperateForest => 1.3,
        Biome.Desert => 1.2,
        Biome.Tundra or Biome.Ice => 1.1,
        _ => 1
    };

    /// <summary>
    /// Ventaja del tipo de camino: por un camino real se avanza más rápido que por un sendero,
    /// y por un sendero más que campo traviesa (null: sin camino).
    /// </summary>
    public static double RouteFactor(RouteKind? kind) => kind switch
    {
        RouteKind.Highway => 0.85,
        RouteKind.Road => 1,
        RouteKind.Trail => 1.15,
        _ => 1.25
    };

    /// <summary>
    /// Kilómetros y días de un recorrido en píxeles del mapa. El terreno de cada tramo se mira en
    /// la celda de su punto medio.
    /// </summary>
    public static TravelEstimate Estimate(World world, IReadOnlyList<(double X, double Y)> path, double kmPerPixel, RouteKind? kind)
    {
        double km = 0, effort = 0;
        var hint = -1;
        for (var i = 1; i < path.Count; i++)
        {
            var length = Math.Sqrt(Math.Pow(path[i].X - path[i - 1].X, 2) + Math.Pow(path[i].Y - path[i - 1].Y, 2)) * kmPerPixel;
            var cell = world.Mesh.FindCell((path[i].X + path[i - 1].X) / 2, (path[i].Y + path[i - 1].Y) / 2, hint);
            hint = cell;
            var slowdown = cell >= 0 && world.IsLand(cell) ? TerrainSlowdown(world.Biomes[cell]) : 1;
            km += length;
            effort += length * slowdown;
        }

        effort *= RouteFactor(kind);
        return new TravelEstimate(km, effort / WalkKmPerDay, effort / HorseKmPerDay, effort / CartKmPerDay);
    }
}

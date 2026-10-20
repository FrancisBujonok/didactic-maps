using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MapaMundos.Tests;

/// <summary>Ayudas compartidas por las pruebas: parámetros fijos, mundos y figuras de prueba.</summary>
internal static class TestWorlds
{
    /// <summary>Parámetros con todas las semillas fijas: siempre el mismo mapa.</summary>
    public static GeneratorSettings Settings(ReliefTemplate template = ReliefTemplate.Continent, int seed = 42) => new()
    {
        ContinentSeed = seed,
        ClimateSeed = 1,
        RiverSeed = 2,
        BiomeSeed = 3,
        Template = template,
        LandRatio = template == ReliefTemplate.Sketch ? 0.4 : ReliefTemplates.Info(template).LandRatio
    };

    public static World Build(GeneratorSettings settings) => MapGenerator.Build(settings, new List<CellEdit>());

    /// <summary>
    /// Elipse como lista plana normalizada [x0, y0, x1, y1, ...], el formato de los dibujos.
    /// Centro y radios en píxeles del mapa (2000 x 1250).
    /// </summary>
    public static double[] Ellipse(double cx, double cy, double rx, double ry, int points = 40) =>
        Enumerable.Range(0, points)
            .SelectMany(i =>
            {
                var angle = 2 * Math.PI * i / points;
                return new[]
                {
                    (cx + rx * Math.Cos(angle)) / MapGenerator.Width,
                    (cy + ry * Math.Sin(angle)) / MapGenerator.Height
                };
            })
            .ToArray();

    /// <summary>
    /// "Huella" de un mundo: un resumen corto (SHA-256) de la altura, el bioma y el río de cada
    /// celda. Dos mundos con la misma huella son iguales celda por celda. Las alturas se redondean
    /// a 9 decimales para que una diferencia ínfima de redondeo entre procesadores no la cambie.
    /// </summary>
    public static string Fingerprint(World world)
    {
        var text = new StringBuilder();
        for (var i = 0; i < world.Mesh.Count; i++)
        {
            text.Append(Math.Round(world.Elevation[i], 9).ToString("R", CultureInfo.InvariantCulture))
                .Append(' ').Append((int)world.Biomes[i])
                .Append(' ').Append(world.RiverSize(i))
                .Append('\n');
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexString(hash)[..16];
    }
}

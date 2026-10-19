namespace MapaMundos.Generation;

/// <summary>
/// Etapa 2, clima: temperatura y humedad de cada celda.
///
/// Temperatura: depende de la latitud (más frío hacia el polo) y de la altura (las montañas
/// son más frías). El mapa cubre una franja de 40 grados en el hemisferio norte; la semilla
/// elige a qué latitud está esa franja.
///
/// Humedad: alta cerca del agua y baja tierra adentro, más manchas de ruido para que no
/// queden anillos perfectos alrededor de la costa.
/// </summary>
public static class ClimateStage
{
    private const double LatitudeSpan = 40;  // grados que cubre el mapa de arriba abajo
    private const double PolarLatitude = 75; // a esta latitud la temperatura llega a 0
    private const double AltitudeCooling = 0.4;

    public static void Apply(World world, int seed)
    {
        var mesh = world.Mesh;
        var rng = new Rng(seed);
        var centerLatitude = rng.Range(5, 50);
        var temperatureNoise = new PerlinNoise(seed + 1);
        var moistureNoise = new PerlinNoise(seed + 2);

        for (var i = 0; i < mesh.Count; i++)
        {
            var nx = mesh.X[i] / mesh.Height;
            var ny = mesh.Y[i] / mesh.Height;

            // Arriba del mapa es el norte: más latitud, más frío.
            var latitude = centerLatitude + LatitudeSpan / 2 - LatitudeSpan * (mesh.Y[i] / mesh.Height);
            var temperature = 1 - Math.Clamp(latitude, 0, PolarLatitude) / PolarLatitude;
            if (world.IsLand(i))
                temperature -= world.Elevation[i] * AltitudeCooling;
            temperature += temperatureNoise.Sample(nx * 4, ny * 4) * 0.05;
            world.Temperature[i] = Math.Clamp(temperature, 0, 1);

            if (world.Water[i])
            {
                world.Moisture[i] = 1;
                continue;
            }

            // Distancia a la costa medida en "altos de mapa", para que no dependa del detalle
            // elegido: con celdas más chicas hay más celdas entre la costa y el centro.
            var inland = world.CoastDistance[i] * mesh.Spacing / mesh.Height;
            var nearWater = Math.Exp(-inland * 5); // 1 en la costa, cae rápido tierra adentro
            var patches = Math.Clamp(moistureNoise.Fractal(nx * 3, ny * 3, 3) * 0.9 + 0.5, 0, 1);
            world.Moisture[i] = Math.Clamp(0.45 * nearWater + 0.6 * patches - 0.2, 0, 1);
        }
    }
}

using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>
/// Los datos del mundo generado: un valor por celda en cada arreglo (el índice es la celda).
/// Cada etapa lee lo que dejaron las anteriores y completa lo suyo.
/// </summary>
public sealed class World
{
    public World(Mesh mesh)
    {
        Mesh = mesh;
        var n = mesh.Count;
        Elevation = new double[n];
        Water = new bool[n];
        IsLake = new bool[n];
        Temperature = new double[n];
        Moisture = new double[n];
        Downstream = new int[n];
        Flux = new double[n];
        Biomes = new Biome[n];
        Array.Fill(Downstream, -1);
    }

    public Mesh Mesh { get; }

    // ---- Etapa 1: continente

    /// <summary>Altura: 0 es el nivel del mar, 1 la cima más alta, -1 el fondo más hondo.</summary>
    public double[] Elevation { get; }

    /// <summary>True si la celda es agua (mar o lago).</summary>
    public bool[] Water { get; }

    /// <summary>True si es agua encerrada por tierra, sin salida al mar abierto.</summary>
    public bool[] IsLake { get; }

    /// <summary>Celdas de distancia a la costa (0 en las celdas que la tocan).</summary>
    public int[] CoastDistance { get; set; } = Array.Empty<int>();

    // ---- Etapa 2: clima

    /// <summary>Temperatura de 0 (polar) a 1 (ecuatorial).</summary>
    public double[] Temperature { get; }

    /// <summary>Humedad de 0 (seco) a 1 (empapado).</summary>
    public double[] Moisture { get; }

    // ---- Etapa 3: ríos

    /// <summary>Celda hacia la que drena el agua, o -1 si es mar.</summary>
    public int[] Downstream { get; }

    /// <summary>Agua acumulada que pasa por la celda (lluvia propia más la de río arriba).</summary>
    public double[] Flux { get; }

    /// <summary>Caudal desde el cual una celda se dibuja como río.</summary>
    public double RiverThreshold { get; set; }

    // ---- Etapa 4: biomas

    public Biome[] Biomes { get; }

    /// <summary>True si la celda es tierra (ni mar ni lago).</summary>
    public bool IsLand(int i) => !Water[i];

    /// <summary>True si la celda es mar abierto.</summary>
    public bool IsOcean(int i) => Water[i] && !IsLake[i];

    /// <summary>Tamaño del río que pasa por la celda: -1 ninguno, 0 arroyo, 1 río, 2 río caudaloso.</summary>
    public int RiverSize(int i)
    {
        if (IsOcean(i) || Flux[i] < RiverThreshold) return -1;
        var ratio = Flux[i] / RiverThreshold;
        return ratio < 4 ? 0 : ratio < 12 ? 1 : 2;
    }

    // ---- Unidades para mostrar. El generador trabaja todo entre 0 y 1; estas escalas son
    //      inventadas para que los números se lean como los de un mundo real.

    /// <summary>Altura en metros sobre el nivel del mar (negativa bajo el agua).</summary>
    public double ElevationMeters(int i) => Elevation[i] * (Elevation[i] > 0 ? 4500 : 5000);

    /// <summary>
    /// Temperatura media anual en grados: 0 son -15 °C y 1 son 35 °C. Con esta escala los
    /// límites de la tabla de biomas caen cerca de los reales: hielo bajo -9 °C, tundra y taiga
    /// hasta 0 °C, templado hasta 15 °C y tropical por encima.
    /// </summary>
    public double TemperatureCelsius(int i) => -15 + 50 * Temperature[i];
}

namespace MapaMundos.Tests;

/// <summary>El generador: semillas, porcentaje de tierra y mapas conocidos.</summary>
public class GeneratorTests
{
    [Fact]
    public void SameSettings_GiveSameWorld()
    {
        var first = TestWorlds.Build(TestWorlds.Settings());
        var second = TestWorlds.Build(TestWorlds.Settings());

        Assert.Equal(first.Elevation, second.Elevation);
        Assert.Equal(first.Biomes, second.Biomes);
        Assert.Equal(first.Flux, second.Flux);
    }

    [Fact]
    public void OtherContinentSeed_GivesOtherWorld()
    {
        var first = TestWorlds.Build(TestWorlds.Settings(seed: 42));
        var second = TestWorlds.Build(TestWorlds.Settings(seed: 43));

        Assert.NotEqual(first.Elevation, second.Elevation);
    }

    [Fact]
    public void ClimateSeed_DoesNotChangeTheLand()
    {
        // Una semilla por etapa: cambiar el clima no puede mover la costa ni el relieve.
        var before = TestWorlds.Settings();
        var after = TestWorlds.Settings();
        after.ClimateSeed = 999;

        Assert.Equal(TestWorlds.Build(before).Elevation, TestWorlds.Build(after).Elevation);
    }

    /// <summary>
    /// El nivel del mar se elige por cuantil: el porcentaje de tierra pedido se cumple en todas
    /// las plantillas (contando las celdas que no son borde, que siempre es mar).
    /// </summary>
    [Theory]
    [InlineData(ReliefTemplate.Continent, 0.4)]
    [InlineData(ReliefTemplate.Continents, 0.35)]
    [InlineData(ReliefTemplate.Pangea, 0.55)]
    [InlineData(ReliefTemplate.Archipelago, 0.2)]
    [InlineData(ReliefTemplate.VolcanicIsland, 0.15)]
    [InlineData(ReliefTemplate.Peninsula, 0.3)]
    [InlineData(ReliefTemplate.Isthmus, 0.3)]
    [InlineData(ReliefTemplate.Mediterranean, 0.6)]
    [InlineData(ReliefTemplate.Atoll, 0.05)]
    [InlineData(ReliefTemplate.Shattered, 0.25)]
    public void LandRatio_IsRespected(ReliefTemplate template, double landRatio)
    {
        var settings = TestWorlds.Settings(template);
        settings.LandRatio = landRatio;
        var world = TestWorlds.Build(settings);

        var inner = Enumerable.Range(0, world.Mesh.Count).Where(i => !world.Mesh.IsBorder[i]).ToList();
        var land = inner.Count(world.IsLand) / (double)inner.Count;

        Assert.InRange(land, landRatio - 0.005, landRatio + 0.005);
    }

    [Fact]
    public void Border_IsAlwaysSea()
    {
        var world = TestWorlds.Build(TestWorlds.Settings(ReliefTemplate.Pangea));

        for (var i = 0; i < world.Mesh.Count; i++)
        {
            if (world.Mesh.IsBorder[i])
                Assert.True(world.Water[i], $"La celda de borde {i} es tierra.");
        }
    }

    /// <summary>
    /// "Mismas semillas, mismo mapa", también entre versiones del programa. Cada huella es la de
    /// un mapa generado con el código de hoy. Si esta prueba falla después de un cambio, ese
    /// cambio altera los mapas que ya generaste: "Generar" con las mismas semillas daría otra
    /// cosa. Si el cambio es a propósito, actualizá la huella (el mensaje de error dice la nueva)
    /// y anotalo en CLAUDE.md.
    /// </summary>
    [Theory]
    [InlineData(ReliefTemplate.Continent, 42, "CE948883CF53FE20")]
    [InlineData(ReliefTemplate.Archipelago, 7, "88DA5B2BEBF72A53")]
    [InlineData(ReliefTemplate.Mediterranean, 99, "B71409B5DADDCB1C")]
    public void KnownMaps_DoNotChange(ReliefTemplate template, int seed, string fingerprint)
    {
        var world = TestWorlds.Build(TestWorlds.Settings(template, seed));

        Assert.Equal(fingerprint, TestWorlds.Fingerprint(world));
    }
}

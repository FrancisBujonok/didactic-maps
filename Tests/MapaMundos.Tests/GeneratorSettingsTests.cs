namespace MapaMundos.Tests;

/// <summary>Los parámetros del generador: copias, limpieza de datos guardados y avisos.</summary>
public class GeneratorSettingsTests
{
    private static readonly double[] Triangle = { 0.1, 0.1, 0.5, 0.1, 0.3, 0.5 };

    [Fact]
    public void Clone_DoesNotShareTheDrawing()
    {
        var original = TestWorlds.Settings();
        original.Sketch = new List<double[]> { (double[])Triangle.Clone() };

        var copy = original.Clone();
        copy.Sketch[0][0] = 0.9;
        copy.Sketch.Add(Triangle);

        // Si Clone copiara solo la referencia a la lista, el original cambiaría también.
        Assert.Single(original.Sketch);
        Assert.Equal(0.1, original.Sketch[0][0]);
    }

    [Fact]
    public void Normalize_OldMapWithOtherTemplate_DropsTheUnusedDrawing()
    {
        // Mapa guardado antes de los retoques: plantilla Continente con un dibujo viejo sin usar.
        var old = TestWorlds.Settings();
        old.Sketch = new List<double[]> { Triangle };
        old.Retouches = false;

        old.Normalize();

        Assert.Empty(old.Sketch);
    }

    [Theory]
    [InlineData(ReliefTemplate.Sketch, false)]     // con Dibujo, el dibujo es la costa
    [InlineData(ReliefTemplate.Continent, true)]   // con la marca, son retoques
    public void Normalize_KeepsDrawingsThatAreUsed(ReliefTemplate template, bool retouches)
    {
        var settings = TestWorlds.Settings(template);
        settings.Sketch = new List<double[]> { Triangle };
        settings.Retouches = retouches;

        settings.Normalize();

        Assert.Single(settings.Sketch);
    }

    [Fact]
    public void Normalize_ClampsOutOfRangeValues()
    {
        var settings = TestWorlds.Settings();
        settings.LandRatio = 3;
        settings.CellSize = 7; // no es un nivel de detalle de la interfaz
        settings.SketchFidelity = -1;

        settings.Normalize();

        Assert.Equal(0.7, settings.LandRatio);
        Assert.Equal(18, settings.CellSize);
        Assert.Equal(0, settings.SketchFidelity);
    }

    [Fact]
    public void ChangesMesh_OnlyForContinentSeedOrDetail()
    {
        var before = TestWorlds.Settings();

        var otherClimate = before.Clone();
        otherClimate.ClimateSeed = 77;
        var otherDetail = before.Clone();
        otherDetail.CellSize = 13;

        Assert.False(MapGenerator.ChangesMesh(before, otherClimate));
        Assert.True(MapGenerator.ChangesMesh(before, otherDetail));
    }

    [Fact]
    public void ChangesCoast_WhenTheDrawingChanges_ButNotFidelityAlone()
    {
        var before = TestWorlds.Settings();

        // Sin nada dibujado, la fidelidad no mueve la costa.
        var otherFidelity = before.Clone();
        otherFidelity.SketchFidelity = 0.2;
        Assert.False(MapGenerator.ChangesCoast(before, otherFidelity));

        // Una isla agregada, sí.
        var withIsland = before.Clone();
        withIsland.Sketch.Add(Triangle);
        Assert.True(MapGenerator.ChangesCoast(before, withIsland));
    }
}

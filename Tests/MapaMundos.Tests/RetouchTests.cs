namespace MapaMundos.Tests;

/// <summary>Retoques dibujados sobre una plantilla común (ReliefShape.Overlay).</summary>
public class RetouchTests
{
    [Fact]
    public void WithoutDrawings_TheWorldDoesNotChange()
    {
        // La marca de retoques sola, sin nada dibujado, no puede cambiar ni una celda.
        var plain = TestWorlds.Settings();
        var marked = TestWorlds.Settings();
        marked.Retouches = true;

        Assert.Equal(TestWorlds.Build(plain).Elevation, TestWorlds.Build(marked).Elevation);
    }

    [Fact]
    public void AddedIsland_IsLand_AndFarCellsStayTheSame()
    {
        var settings = TestWorlds.Settings();
        var before = TestWorlds.Build(settings);
        var mesh = before.Mesh;

        // La isla va en el punto de mar más alejado de la costa (lejos del borde del mapa).
        var sea = Enumerable.Range(0, mesh.Count)
            .Where(i => before.Water[i] && !mesh.IsBorder[i])
            .Where(i => mesh.X[i] is > 300 and < 1700 && mesh.Y[i] is > 250 and < 1000)
            .MaxBy(i => before.CoastDistance[i]);
        var island = TestWorlds.Ellipse(mesh.X[sea], mesh.Y[sea], 110, 80);

        settings.Retouches = true;
        settings.SketchFidelity = 0.85;
        settings.Sketch = new List<double[]> { island };
        var after = TestWorlds.Build(settings);

        // Adentro del dibujo: casi todo tierra (el ruido puede comer un poco el borde).
        var polygon = Sketches.ToPixels(island, MapGenerator.Width, MapGenerator.Height);
        var inside = Enumerable.Range(0, mesh.Count).Where(i => Sketches.Contains(polygon, mesh.X[i], mesh.Y[i])).ToList();
        Assert.True(inside.Count(after.IsLand) >= 0.9 * inside.Count,
            $"Solo {inside.Count(after.IsLand)} de {inside.Count} celdas de la isla son tierra.");

        // Lejos del dibujo (más que el alcance de 240 px): la altura no cambia en ninguna celda.
        var closed = polygon.Append(polygon[0]).ToArray();
        var far = Enumerable.Range(0, mesh.Count)
            .Where(i => Sketches.DistanceToPolyline(closed, mesh.X[i], mesh.Y[i]) > 250)
            .ToList();
        Assert.All(far, i => Assert.Equal(before.Elevation[i], after.Elevation[i]));
    }

    [Fact]
    public void DrawnWater_ErasesLand()
    {
        var settings = TestWorlds.Settings();
        var before = TestWorlds.Build(settings);
        var mesh = before.Mesh;

        // El agua va sobre la tierra más alejada de la costa: el centro del continente.
        var land = Enumerable.Range(0, mesh.Count).Where(before.IsLand).MaxBy(i => before.CoastDistance[i]);
        var water = TestWorlds.Ellipse(mesh.X[land], mesh.Y[land], 140, 100);

        settings.Retouches = true;
        settings.SketchFidelity = 0.85;
        settings.SketchFeatures = new List<SketchFeature> { new() { Kind = SketchFeatureKind.Water, Points = water } };
        var after = TestWorlds.Build(settings);

        var polygon = Sketches.ToPixels(water, MapGenerator.Width, MapGenerator.Height);
        var inside = Enumerable.Range(0, mesh.Count).Where(i => Sketches.Contains(polygon, mesh.X[i], mesh.Y[i])).ToList();
        Assert.True(inside.Count(i => after.Water[i]) >= 0.9 * inside.Count,
            $"Solo {inside.Count(i => after.Water[i])} de {inside.Count} celdas del agua dibujada son agua.");
        // Encerrada en tierra, queda como lago (no como mar).
        Assert.Contains(inside, i => after.IsLake[i]);
    }
}

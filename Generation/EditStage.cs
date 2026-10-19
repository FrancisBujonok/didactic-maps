using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>
/// Etapa de ediciones a mano. Se aplica en dos momentos del armado del mundo:
///
/// 1. Alturas, justo después del continente: así el clima y los ríos se calculan con el
///    terreno ya editado (si se levanta una montaña, los ríos la rodean).
/// 2. Biomas, al final de todo: lo elegido a mano gana sobre lo que decide la tabla.
/// </summary>
public static class EditStage
{
    /// <summary>
    /// Suma a cada celda editada su cambio de altura. Devuelve true si cambió alguna, para que
    /// quien llama sepa que hay que recalcular mar, lagos y costa.
    /// </summary>
    public static bool ApplyElevation(World world, IReadOnlyList<CellEdit> edits)
    {
        var changed = false;
        foreach (var edit in edits)
        {
            if (!IsValid(world, edit) || edit.ElevationDelta == 0) continue;
            world.Elevation[edit.Cell] = Math.Clamp(world.Elevation[edit.Cell] + edit.ElevationDelta, -1, 1);
            changed = true;
        }
        return changed;
    }

    /// <summary>Pone el bioma elegido a mano en las celdas que siguen siendo tierra.</summary>
    public static void ApplyBiomes(World world, IReadOnlyList<CellEdit> edits)
    {
        foreach (var edit in edits)
        {
            if (IsValid(world, edit) && edit.Biome is { } biome && world.IsLand(edit.Cell))
                world.Biomes[edit.Cell] = biome;
        }
    }

    // Una edición de un archivo viejo o de otra malla puede apuntar a una celda que no existe,
    // o a una del borde, que no se dibuja.
    private static bool IsValid(World world, CellEdit edit) =>
        edit.Cell >= 0 && edit.Cell < world.Mesh.Count && !world.Mesh.IsBorder[edit.Cell];
}

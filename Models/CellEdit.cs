namespace MapaMundos.Models;

/// <summary>
/// Un cambio hecho a mano sobre una celda del mapa generado. No se guarda el mundo entero,
/// solo estas diferencias: al generar, el mundo se rearma con las semillas y después se le
/// aplican las ediciones, como una etapa más.
/// </summary>
public class CellEdit
{
    /// <summary>Índice de la celda en la malla. Solo vale mientras no cambien la semilla del continente ni el detalle.</summary>
    public int Cell { get; set; }

    /// <summary>Cuánto se sumó a la altura generada (la altura va de -1 a 1; 0 es el nivel del mar).</summary>
    public double ElevationDelta { get; set; }

    /// <summary>Bioma elegido a mano, o null para dejar el que decide el generador. Solo se aplica en tierra.</summary>
    public Biome? Biome { get; set; }

    /// <summary>True si la edición ya no cambia nada y se puede descartar.</summary>
    public bool IsEmpty => Math.Abs(ElevationDelta) < 1e-9 && Biome is null;
}

namespace MapaMundos.Models;

/// <summary>
/// Un reino (o región, provincia, territorio): un grupo de celdas del mapa generado con
/// nombre y color. Cada celda pertenece a un reino como máximo.
/// </summary>
public class Region
{
    /// <summary>
    /// Colores que se asignan a los reinos nuevos. Son tonos medios y apagados porque se
    /// dibujan semitransparentes sobre el terreno y tienen que distinguirse entre sí.
    /// </summary>
    public static readonly string[] Palette =
    {
        "#B5523B", "#3E6E9C", "#5E8C4A", "#C08A2E", "#7A5A9A", "#2F8A82", "#A8566F", "#6B6B3A"
    };

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Reino";
    public string Color { get; set; } = Palette[0];

    /// <summary>
    /// Índices de las celdas del reino. Como las ediciones a mano, solo valen mientras no
    /// cambien la semilla del continente ni el detalle.
    /// </summary>
    public List<int> Cells { get; set; } = new();
}

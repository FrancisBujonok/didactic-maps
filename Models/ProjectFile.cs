namespace MapaMundos.Models;

/// <summary>
/// Formato del archivo .mundo.json que se exporta e importa:
/// el proyecto más la imagen base embebida como "data URL".
/// </summary>
public class ProjectFile
{
    public int Version { get; set; } = 1;
    public MapProject Project { get; set; } = new();
    public string? ImageDataUrl { get; set; }
}

/// <summary>
/// Lo que devuelve el selector de archivos de JavaScript (files.js).
/// Content es un data URL si es una imagen, o el texto del archivo si se leyó como texto.
/// </summary>
public class PickedFile
{
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
}

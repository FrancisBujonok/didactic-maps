using System.Text.Json;
using MapaMundos.Generation;
using MapaMundos.Models;
using Microsoft.JSInterop;

namespace MapaMundos.Services;

/// <summary>
/// Guarda el proyecto abierto y se ocupa de persistirlo (IndexedDB) y de exportarlo/importarlo.
/// Los componentes no tocan el almacenamiento: le piden las cosas a este servicio.
/// </summary>
public class ProjectService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IJSRuntime _js;
    private IJSObjectReference? _storage; // js/storage.js
    private IJSObjectReference? _files;   // js/files.js
    private CancellationTokenSource? _saveCts;
    private World? _world; // mundo generado en memoria (ver GetWorld)

    public ProjectService(IJSRuntime js) => _js = js;

    public MapProject Project { get; private set; } = new();
    public string? ImageDataUrl { get; private set; }
    public bool Loaded { get; private set; }
    public string SaveStatus { get; private set; } = "";

    /// <summary>Se dispara cuando cambia SaveStatus, para que la interfaz se actualice.</summary>
    public event Action? StatusChanged;

    // ---------------------------------------------------------------- carga

    /// <summary>Carga lo guardado en el navegador. Solo trabaja la primera vez que se llama.</summary>
    public async Task InitAsync()
    {
        if (Loaded) return;

        _files = await _js.InvokeAsync<IJSObjectReference>("import", "./js/files.js");
        try
        {
            _storage = await _js.InvokeAsync<IJSObjectReference>("import", "./js/storage.js");

            var json = await _storage.InvokeAsync<string?>("get", "project");
            if (json is not null)
                Project = JsonSerializer.Deserialize<MapProject>(json, JsonOptions) ?? new MapProject();

            ImageDataUrl = await _storage.InvokeAsync<string?>("get", "image");
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            Project = new MapProject();
            ImageDataUrl = null;
            SetStatus("No se pudo leer lo guardado: se empieza con un proyecto vacío.");
        }

        Normalize();
        Loaded = true;
    }

    /// <summary>Deja el proyecto en un estado válido (también sirve para archivos importados).</summary>
    private void Normalize()
    {
        Project.Layers ??= new();
        Project.Pins ??= new();

        if (Project.Layers.Count == 0)
            Project.Layers.Add(new MapLayer { Name = "Lugares" });

        foreach (var layer in Project.Layers)
        {
            if (!MapLayer.IsHexColor(layer.Color))
                layer.Color = MapLayer.DefaultColor;
        }

        // Un pin que apunta a una capa que no existe no se puede mostrar: se descarta.
        var layerIds = Project.Layers.Select(l => l.Id).ToHashSet();
        Project.Pins.RemoveAll(p => !layerIds.Contains(p.LayerId));

        foreach (var pin in Project.Pins)
        {
            pin.X = Math.Clamp(pin.X, 0, 1);
            pin.Y = Math.Clamp(pin.Y, 0, 1);
        }

        Project.Generator?.Normalize();
    }

    // ---------------------------------------------------------------- capas

    /// <summary>Crea una capa nueva arriba de todas.</summary>
    public MapLayer AddLayer()
    {
        var layer = new MapLayer
        {
            Name = $"Capa {Project.Layers.Count + 1}",
            Color = MapLayer.Palette[Project.Layers.Count % MapLayer.Palette.Length]
        };
        Project.Layers.Insert(0, layer);
        RequestSave();
        return layer;
    }

    public void RemoveLayer(Guid id)
    {
        if (Project.Layers.Count <= 1) return; // siempre tiene que quedar al menos una
        Project.Layers.RemoveAll(l => l.Id == id);
        Project.Pins.RemoveAll(p => p.LayerId == id);
        RequestSave();
    }

    /// <summary>Mueve la capa en la lista: delta -1 la sube, +1 la baja.</summary>
    public void MoveLayer(MapLayer layer, int delta)
    {
        var from = Project.Layers.IndexOf(layer);
        if (from < 0) return;

        var to = Math.Clamp(from + delta, 0, Project.Layers.Count - 1);
        if (to == from) return;

        Project.Layers.RemoveAt(from);
        Project.Layers.Insert(to, layer);
        RequestSave();
    }

    // ---------------------------------------------------------------- pines

    public MapPin AddPin(Guid layerId, double x, double y)
    {
        var pin = new MapPin
        {
            LayerId = layerId,
            X = Math.Clamp(x, 0, 1),
            Y = Math.Clamp(y, 0, 1)
        };
        Project.Pins.Add(pin);
        RequestSave();
        return pin;
    }

    public void MovePin(Guid id, double x, double y)
    {
        var pin = Project.Pins.FirstOrDefault(p => p.Id == id);
        if (pin is null) return;

        pin.X = Math.Clamp(x, 0, 1);
        pin.Y = Math.Clamp(y, 0, 1);
        RequestSave();
    }

    public void RemovePin(Guid id)
    {
        Project.Pins.RemoveAll(p => p.Id == id);
        RequestSave();
    }

    // ------------------------------------------------------- archivos / imagen

    /// <summary>Abre el selector de archivos del navegador. Devuelve null si se cancela.</summary>
    public async Task<PickedFile?> PickFileAsync(string accept, bool asText)
    {
        if (_files is null) return null;
        return await _files.InvokeAsync<PickedFile?>("pickFile", accept, asText);
    }

    /// <summary>Usa el archivo elegido como imagen base. Devuelve false si no es una imagen.</summary>
    public async Task<bool> SetImageAsync(PickedFile file)
    {
        if (!file.Content.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return false;

        ImageDataUrl = file.Content;
        Project.Generator = null; // la imagen ya no es la generada
        _world = null;
        RequestSave();
        await SaveImageAsync();
        return true;
    }

    /// <summary>Usa un mapa generado como imagen base y guarda con qué parámetros se hizo.</summary>
    public async Task SetGeneratedImageAsync(string dataUrl, GeneratorSettings settings, World world)
    {
        ImageDataUrl = dataUrl;
        Project.Generator = settings;
        _world = world;
        RequestSave();
        await SaveImageAsync();
    }

    /// <summary>True si el mundo ya está en memoria (consultarlo no va a demorar).</summary>
    public bool WorldReady => _world is not null;

    /// <summary>
    /// El mundo generado, para consultar sus celdas, o null si la imagen no es generada.
    /// No se guarda en el navegador: si falta (por ejemplo, después de recargar la página) se
    /// rearma con las semillas del proyecto. Da el mismo mundo porque el generador es determinista.
    /// </summary>
    public World? GetWorld()
    {
        if (Project.Generator is null) return null;
        _world ??= MapGenerator.Build(Project.Generator);
        return _world;
    }

    public async Task ExportAsync()
    {
        if (_files is null) return;

        var file = new ProjectFile { Project = Project, ImageDataUrl = ImageDataUrl };
        var json = JsonSerializer.Serialize(file, JsonOptions);
        await _files.InvokeVoidAsync("download", $"{Slug(Project.Name)}.mundo.json", json);
    }

    /// <summary>Reemplaza el proyecto actual por el contenido de un .mundo.json.</summary>
    public async Task<(bool Ok, string Message)> ImportAsync(string json)
    {
        try
        {
            var file = JsonSerializer.Deserialize<ProjectFile>(json, JsonOptions);
            if (file?.Project is null)
                return (false, "El archivo no contiene un proyecto válido.");

            Project = file.Project;
            _world = null;
            ImageDataUrl = file.ImageDataUrl is { } url &&
                           url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                ? url
                : null;
            Normalize();

            await SaveNowAsync();
            await SaveImageAsync();
            return (true, $"Proyecto \"{Project.Name}\" importado.");
        }
        catch (JsonException)
        {
            return (false, "El archivo no es un JSON válido de este programa.");
        }
    }

    /// <summary>Empieza un proyecto vacío.</summary>
    public async Task ResetAsync()
    {
        _saveCts?.Cancel();
        Project = new MapProject();
        ImageDataUrl = null;
        _world = null;
        Normalize();

        await SaveNowAsync();
        await SaveImageAsync();
    }

    // ------------------------------------------------------------- guardado

    /// <summary>
    /// Pide guardar el proyecto. Si llegan varios cambios seguidos (por ejemplo al escribir),
    /// espera a que pare la ráfaga y guarda una sola vez (debounce).
    /// </summary>
    public void RequestSave()
    {
        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        _ = SaveLaterAsync(_saveCts.Token);
    }

    private async Task SaveLaterAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(400, token);
            await SaveNowAsync();
        }
        catch (OperationCanceledException)
        {
            // Llegó un cambio más nuevo: ese pedido se encarga de guardar.
        }
    }

    public async Task SaveNowAsync()
    {
        if (_storage is null) return;
        try
        {
            await _storage.InvokeVoidAsync("set", "project", JsonSerializer.Serialize(Project, JsonOptions));
            SetStatus("Guardado en este navegador");
        }
        catch (JSException ex)
        {
            SetStatus($"No se pudo guardar: {ex.Message}");
        }
    }

    // La imagen se guarda aparte del proyecto y solo cuando cambia: es lo más pesado.
    private async Task SaveImageAsync()
    {
        if (_storage is null) return;
        try
        {
            if (ImageDataUrl is null)
                await _storage.InvokeVoidAsync("remove", "image");
            else
                await _storage.InvokeVoidAsync("set", "image", ImageDataUrl);

            SetStatus("Guardado en este navegador");
        }
        catch (JSException ex)
        {
            SetStatus($"No se pudo guardar la imagen: {ex.Message}");
        }
    }

    private void SetStatus(string text)
    {
        SaveStatus = text;
        StatusChanged?.Invoke();
    }

    private static string Slug(string name)
    {
        var slug = string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        return slug.Length == 0 ? "mapa" : slug;
    }
}

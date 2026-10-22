using MapaMundos.Wiki;

namespace MapaMundos.Tests;

/// <summary>Nombres y menciones entre fichas (Wiki/WikiLinks.cs).</summary>
public class WikiLinksTests
{
    // Un mundo chico: un reino, una ciudad con ficha y un personaje suelto que los menciona.
    private readonly MapProject _project = new();
    private readonly Region _kingdom = new() { Name = "Valdoria" };
    private readonly MapPin _city = new() { Title = "Orm" };
    private readonly WikiEntry _hero = new() { Title = "Aldara", Text = "Nació en [[orm]], al norte de [[Valdoria|su reino]]." };

    public WikiLinksTests()
    {
        // xUnit crea una instancia nueva de la clase para cada prueba: este constructor arma
        // un mundo limpio cada vez, y ninguna prueba ve los cambios de otra.
        _project.Regions.Add(_kingdom);
        _project.Pins.Add(_city);
        _project.Entries.Add(_hero);
        _project.Entries.Add(new WikiEntry { PinId = _city.Id, Text = "Ciudad portuaria de [[Valdoria]]." });
    }

    [Fact]
    public void Resolve_IgnoresCaseAndSpaces()
    {
        Assert.Equal(WikiTarget.Pin(_city.Id), WikiLinks.Resolve(_project, "  ORM "));
        Assert.Equal(WikiTarget.Region(_kingdom.Id), WikiLinks.Resolve(_project, "valdoria"));
        Assert.Equal(WikiTarget.Entry(_hero.Id), WikiLinks.Resolve(_project, "Aldara"));
    }

    [Fact]
    public void Resolve_UnknownName_IsNull()
    {
        Assert.Null(WikiLinks.Resolve(_project, "Atlántida"));
    }

    [Fact]
    public void Resolve_LooseEntryWinsOverPinWithTheSameName()
    {
        var village = new MapPin { Title = "Aldara" };
        _project.Pins.Add(village);

        Assert.Equal(WikiTarget.Entry(_hero.Id), WikiLinks.Resolve(_project, "Aldara"));
    }

    [Fact]
    public void NameOf_LinkedEntry_UsesThePinTitle()
    {
        // La ficha vinculada no tiene nombre propio: si se renombra el pin, la ficha lo sigue.
        _city.Title = "Orm del Mar";

        Assert.Equal("Orm del Mar", WikiLinks.NameOf(_project, WikiTarget.Pin(_city.Id)));
    }

    [Fact]
    public void EntryOf_PinWithoutText_IsNull()
    {
        var empty = new MapPin { Title = "Torre" };
        _project.Pins.Add(empty);

        Assert.Null(WikiLinks.EntryOf(_project, WikiTarget.Pin(empty.Id)));
    }

    [Fact]
    public void MentionsOf_FindsWhoLinksHere()
    {
        var kingdom = WikiTarget.Region(_kingdom.Id);

        var mentions = WikiLinks.MentionsOf(_project, kingdom);

        // La mencionan la heroína (con texto propio en el enlace) y la ficha de la ciudad.
        Assert.Equal(new[] { WikiTarget.Entry(_hero.Id), WikiTarget.Pin(_city.Id) }, mentions);
    }

    [Fact]
    public void MentionsOf_DoesNotCountItself()
    {
        _hero.Text += " Ver también [[Aldara]].";

        Assert.DoesNotContain(WikiTarget.Entry(_hero.Id), WikiLinks.MentionsOf(_project, WikiTarget.Entry(_hero.Id)));
    }
}

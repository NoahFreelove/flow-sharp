using System.Text.Json;
using System.Text.Json.Nodes;
using Flow.Music.Model;
using Flow.Studio.Model;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class CompositionJsonTests
{
    private static CompositionSnapshot Score()
    {
        var origin = new SourceOrigin("source.flow", 3, 4, 8);
        var note = new NoteEvent(Guid.NewGuid(), "voice", -0.25, 1.0 / 3,
            new('A', 4, 0, 14, 69, 443.5), 0.73, NoteArticulation.Legato, true, 0.1, 13, new(1, 3), origin);
        var seq = new SequenceSnapshot(Guid.NewGuid(), "lead", 4, [note, new(Guid.NewGuid(), "voice", 1, 1, null)],
            [new(Guid.NewGuid(), 0, 4, 4, 4, false)]);
        var section = new SectionSnapshot(Guid.NewGuid(), "verse", new(91, -0.25, 0.5, 0.2, 8, true), [seq], origin);
        return new(Guid.NewGuid(), [new(Guid.NewGuid(), section, 2), new(Guid.NewGuid(), section, 1)]);
    }
    [Fact]
    public void FullMusicalMetadataRoundTripsAndRepeatedSectionsStayShared()
    {
        var score = Score();
        var restored = CompositionJson.Deserialize(CompositionJson.Serialize(score));
        Assert.Equal(CompositionJson.Serialize(score), CompositionJson.Serialize(restored));
        Assert.Same(restored.Placements[0].Section, restored.Placements[1].Section);
        Assert.Equal(score.Placements[0].Section.Sequences[0].Notes, restored.Placements[0].Section.Sequences[0].Notes);
    }
    [Fact]
    public void InvalidReferencesControlsAndSchemaAreRejected()
    {
        var json = JsonNode.Parse(CompositionJson.Serialize(Score()))!;
        json["Placements"]![0]!["Section"] = 999;
        Assert.Throws<JsonException>(() => CompositionJson.Deserialize(json.ToJsonString()));
        json["Placements"]![0]!["Section"] = 0;
        json["Sections"]![0]!["Sequences"]![0]!["Notes"]![0]!["Velocity"] = 2;
        Assert.Throws<JsonException>(() => CompositionJson.Deserialize(json.ToJsonString()));
        json["Version"] = 2;
        Assert.Throws<JsonException>(() => CompositionJson.Deserialize(json.ToJsonString()));
    }
}

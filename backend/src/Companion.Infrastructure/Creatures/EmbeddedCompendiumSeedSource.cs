using Companion.Application.Creatures;

namespace Companion.Infrastructure.Creatures;

/// <summary>The Pocket DM compendium seed embedded in this assembly (Persistence/Seed/creatures.pocketdm.json).</summary>
public sealed class EmbeddedCompendiumSeedSource : ICompendiumSeedSource
{
    private const string ResourceName = "Companion.Infrastructure.Persistence.Seed.creatures.pocketdm.json";

    public string Key => "creatures.pocketdm";

    public byte[] Read()
    {
        using var stream = typeof(EmbeddedCompendiumSeedSource).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso de seed ausente: {ResourceName}.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

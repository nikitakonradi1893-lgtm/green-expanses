using GreenExpanses.Domain;

namespace GreenExpanses.Simulation;

public static class InheritanceScenarioFactory
{
    public static GameState Create(
        ulong worldSeed,
        Money openingCash,
        decimal starterFieldTargetAreaHa = 30m,
        decimal starterFieldMaxDistanceKm = 10m,
        string difficultyProfileId = "normal")
    {
        if (starterFieldTargetAreaHa <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(starterFieldTargetAreaHa));
        }

        if (starterFieldMaxDistanceKm <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(starterFieldMaxDistanceKm));
        }

        var generated = GameStateFactory.Create(worldSeed, difficultyProfileId);
        var candidates = generated.World.Fields
            .Where(field => field.DistanceKm <= starterFieldMaxDistanceKm)
            .ToArray();

        if (candidates.Length == 0)
        {
            candidates = generated.World.Fields.ToArray();
        }

        var starterField = candidates
            .OrderBy(field => Math.Abs(field.Area.Value - starterFieldTargetAreaHa))
            .ThenBy(field => field.DistanceKm)
            .ThenBy(field => field.Id.Value)
            .First();

        return new GameState
        {
            Campaign = generated.Campaign,
            World = generated.World,
            Farm = new FarmState
            {
                PlayerFarmId = generated.Farm.PlayerFarmId,
                OwnedFieldIds = [starterField.Id]
            },
            Economy = new EconomyState(openingCash),
            Simulation = generated.Simulation,
            EventLog = generated.EventLog
        };
    }
}

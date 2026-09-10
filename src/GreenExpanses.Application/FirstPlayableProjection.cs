using GreenExpanses.Domain;

namespace GreenExpanses.Application;

public sealed record FirstPlayableFieldView(
    EntityId FieldId,
    bool IsOwned,
    decimal AreaHa,
    decimal DistanceKm,
    decimal Fertility,
    decimal Ph,
    string Drainage,
    decimal SoilN,
    decimal SoilP,
    decimal SoilK,
    decimal OrganicMatter);

public sealed record FirstPlayableView(
    GameDateTime CurrentDateTime,
    decimal CashRub,
    decimal OwnedAreaHa,
    int OwnedFieldCount,
    int WorldFieldCount,
    IReadOnlyList<FirstPlayableFieldView> Fields);

public static class FirstPlayableProjectionFactory
{
    public static FirstPlayableView Build(GameState state, int fieldLimit = 40)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (fieldLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldLimit));
        }

        var owned = state.Farm.OwnedFieldIds.ToHashSet();
        var fields = state.World.Fields
            .OrderByDescending(field => owned.Contains(field.Id))
            .ThenBy(field => field.DistanceKm)
            .ThenBy(field => field.Area.Value)
            .Take(fieldLimit)
            .Select(field => new FirstPlayableFieldView(
                field.Id,
                owned.Contains(field.Id),
                field.Area.Value,
                field.DistanceKm,
                field.Fertility,
                field.Ph,
                field.DrainageType.Value,
                field.SoilN,
                field.SoilP,
                field.SoilK,
                field.OrganicMatter))
            .ToArray();

        var ownedArea = state.World.Fields
            .Where(field => owned.Contains(field.Id))
            .Sum(field => field.Area.Value);

        return new FirstPlayableView(
            state.CurrentDateTime,
            state.Economy.Cash.Value,
            ownedArea,
            state.Farm.OwnedFieldIds.Count,
            state.World.Fields.Count,
            fields);
    }
}

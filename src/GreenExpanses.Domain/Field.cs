namespace GreenExpanses.Domain;

public sealed record Field
{
    public required EntityId Id { get; init; }
    public required AreaHa Area { get; init; }
    public required decimal DistanceKm { get; init; }
    public required decimal FieldQualityBase { get; init; }
    public required decimal Fertility { get; init; }
    public required decimal Ph { get; init; }
    public required decimal ManagementHistory { get; init; }
    public required CatalogId DrainageType { get; init; }
    public required decimal SoilN { get; init; }
    public required decimal SoilP { get; init; }
    public required decimal SoilK { get; init; }
    public required decimal OrganicMatter { get; init; }

    public void Validate()
    {
        if (Id.Value == Guid.Empty)
        {
            throw new ArgumentException("Field ID cannot be empty.", nameof(Id));
        }

        Validation.InRange(Area.Value, 5m, 400m, nameof(Area));
        Validation.InRange(DistanceKm, 0.5m, 60m, nameof(DistanceKm));
        Validation.InRange(FieldQualityBase, 40m, 100m, nameof(FieldQualityBase));
        Validation.InRange(Fertility, 45m, 100m, nameof(Fertility));
        Validation.InRange(Ph, 5m, 7.8m, nameof(Ph));
        Validation.InRange(ManagementHistory, 20m, 100m, nameof(ManagementHistory));
        Validation.InRange(SoilN, 5m, 95m, nameof(SoilN));
        Validation.InRange(SoilP, 5m, 95m, nameof(SoilP));
        Validation.InRange(SoilK, 5m, 95m, nameof(SoilK));
        Validation.InRange(OrganicMatter, 35m, 100m, nameof(OrganicMatter));
    }
}

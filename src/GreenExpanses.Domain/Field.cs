namespace GreenExpanses.Domain;

public sealed record Field
{
    public required EntityId Id { get; init; }
    public required AreaHa Area { get; init; }
    public required decimal DistanceKm { get; init; }
    public required decimal FieldQualityBase { get; init; }

    public void Validate()
    {
        if (Id.Value == Guid.Empty)
        {
            throw new ArgumentException("Field ID cannot be empty.", nameof(Id));
        }

        Validation.InRange(Area.Value, 5m, 400m, nameof(Area));
        Validation.InRange(DistanceKm, 0.5m, 60m, nameof(DistanceKm));
        Validation.InRange(FieldQualityBase, 40m, 100m, nameof(FieldQualityBase));
    }
}

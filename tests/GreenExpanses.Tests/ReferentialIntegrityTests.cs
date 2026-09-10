using GreenExpanses.Domain;
using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class ReferentialIntegrityTests
{
    [Fact]
    public void EntityRegistry_RejectsDuplicateAndEmptyIds()
    {
        var registry = new EntityRegistry();
        var id = new EntityId(Guid.NewGuid());

        registry.Add(id);

        Assert.True(registry.Contains(id));
        Assert.Throws<InvalidOperationException>(() => registry.Add(id));
        Assert.Throws<ArgumentException>(() => registry.Add(new EntityId(Guid.Empty)));
    }

    [Fact]
    public void OwnedFieldReference_MustExistInWorldRegistry()
    {
        var state = GameStateFactory.Create(42UL);
        var missing = new EntityId(Guid.NewGuid());
        state.Farm.OwnedFieldIds.Add(missing);

        var result = ReferentialIntegrityCheck.Validate(state);

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("farm.field_missing", issue.Code);
        Assert.Equal(missing, issue.MissingEntityId);
        Assert.Equal(state.PlayerFarmId, issue.ReferencingEntityId);
    }

    [Fact]
    public void RegisteredOwnedField_PassesIntegrityCheck()
    {
        var state = GameStateFactory.Create(42UL);
        var fieldId = new EntityId(Guid.NewGuid());
        state.World.FieldRegistry.Add(fieldId);
        state.Farm.OwnedFieldIds.Add(fieldId);

        var result = ReferentialIntegrityCheck.Validate(state);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }
}

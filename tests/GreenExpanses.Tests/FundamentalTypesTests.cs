using GreenExpanses.Domain;
using GreenExpanses.Simulation;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class FundamentalTypesTests
{
    [Theory]
    [InlineData("normal")]
    [InlineData("inheritance_normal")]
    [InlineData("crop_2")]
    public void CatalogId_AcceptsStableSnakeCase(string value)
    {
        Assert.Equal(value, new CatalogId(value).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Normal")]
    [InlineData("two-words")]
    [InlineData("_normal")]
    [InlineData("normal_")]
    [InlineData("two__words")]
    public void CatalogId_RejectsInvalidValues(string value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CatalogId(value));
    }

    [Fact]
    public void GameDateTime_IsTimezoneIndependent()
    {
        var source = new DateTime(2026, 4, 15, 8, 30, 0, DateTimeKind.Utc);
        var gameTime = new GameDateTime(source);

        Assert.Equal(DateTimeKind.Unspecified, gameTime.Value.Kind);
        Assert.Equal("2026-04-15T08:30:00", gameTime.ToString());
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("-1")]
    public void AreaHa_RejectsNegativeValues(string raw)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AreaHa(decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.01")]
    public void Ratio_RejectsValuesOutsideUnitInterval(string raw)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ratio(decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Ratio_AcceptsBoundaries()
    {
        Assert.Equal(0m, new Ratio(0m).Value);
        Assert.Equal(1m, new Ratio(1m).Value);
    }

    [Fact]
    public void Money_UsesDecimalArithmetic()
    {
        var result = new Money(0.1m) + new Money(0.2m);
        Assert.Equal(0.3m, result.Value);
    }

    [Fact]
    public void GameStateFactory_UsesFundamentalTypes()
    {
        var state = GameStateFactory.Create(42UL, "normal");

        Assert.NotEqual(Guid.Empty, state.CampaignId.Value);
        Assert.Equal("normal", state.DifficultyProfileId.Value);
        Assert.Equal(DateTimeKind.Unspecified, state.CurrentDateTime.Value.Kind);
    }
}

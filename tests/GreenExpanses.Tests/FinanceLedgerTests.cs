using GreenExpanses.Domain;
using Xunit;

namespace GreenExpanses.Tests;

public sealed class FinanceLedgerTests
{
    [Fact]
    public void OpeningCash_IsPartOfLedgerInvariant()
    {
        var economy = new EconomyState(new Money(1000m));

        var result = FinanceIntegrityCheck.Validate(economy);

        Assert.True(result.IsValid);
        Assert.Equal(1000m, economy.Cash.Value);
        Assert.Equal(1000m, result.ExpectedCash.Value);
    }

    [Fact]
    public void PostingTransactions_ChangesCashAndPreservesInvariant()
    {
        var economy = new EconomyState(new Money(1000m));
        economy.PostTransaction(Transaction(250m, "crop_sale"));
        economy.PostTransaction(Transaction(-90m, "fuel_purchase"));

        var result = FinanceIntegrityCheck.Validate(economy);

        Assert.True(result.IsValid);
        Assert.Equal(1160m, economy.Cash.Value);
        Assert.Equal(2, economy.Ledger.Count);
    }

    [Fact]
    public void Ledger_RejectsDuplicateTransactionIds()
    {
        var economy = new EconomyState();
        var id = Id();
        economy.PostTransaction(Transaction(10m, "test_credit") with { TransactionId = id });

        Assert.Throws<InvalidOperationException>(() =>
            economy.PostTransaction(Transaction(20m, "test_credit") with { TransactionId = id }));
        Assert.Equal(10m, economy.Cash.Value);
        Assert.Single(economy.Ledger.Entries);
    }

    [Fact]
    public void FailedAppend_DoesNotChangeCash()
    {
        var economy = new EconomyState(new Money(100m));
        var id = Id();
        economy.PostTransaction(Transaction(-25m, "expense") with { TransactionId = id });

        Assert.Throws<InvalidOperationException>(() =>
            economy.PostTransaction(Transaction(-25m, "expense") with { TransactionId = id }));

        Assert.Equal(75m, economy.Cash.Value);
        Assert.True(FinanceIntegrityCheck.Validate(economy).IsValid);
    }

    private static TransactionRecord Transaction(decimal cashDelta, string type) => new()
    {
        TransactionId = Id(),
        GameDateTime = new GameDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)),
        Type = new CatalogId(type),
        CashDelta = new Money(cashDelta),
        DescriptionKey = new CatalogId(type)
    };

    private static EntityId Id() => new(Guid.NewGuid());
}

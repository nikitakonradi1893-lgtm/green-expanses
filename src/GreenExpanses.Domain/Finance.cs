namespace GreenExpanses.Domain;

public sealed record TransactionRecord
{
    public required EntityId TransactionId { get; init; }
    public required GameDateTime GameDateTime { get; init; }
    public required CatalogId Type { get; init; }
    public required Money CashDelta { get; init; }
    public EntityId? CounterpartyId { get; init; }
    public IReadOnlyList<EntityId> RelatedEntityIds { get; init; } = Array.Empty<EntityId>();
    public required CatalogId DescriptionKey { get; init; }
}

public sealed class TransactionLedger
{
    private readonly List<TransactionRecord> _entries = [];
    private readonly HashSet<EntityId> _transactionIds = [];

    public IReadOnlyList<TransactionRecord> Entries => _entries;
    public int Count => _entries.Count;

    internal void Append(TransactionRecord transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (transaction.TransactionId.Value == Guid.Empty)
        {
            throw new ArgumentException("Transaction ID cannot be empty.", nameof(transaction));
        }

        if (!_transactionIds.Add(transaction.TransactionId))
        {
            throw new InvalidOperationException($"Transaction '{transaction.TransactionId}' is already present in the ledger.");
        }

        _entries.Add(transaction);
    }
}

public sealed record FinanceIntegrityResult(bool IsValid, Money ExpectedCash, Money ActualCash)
{
    public string Code => IsValid ? "ok" : "finance.cash_ledger_mismatch";
}

public static class FinanceIntegrityCheck
{
    public static FinanceIntegrityResult Validate(EconomyState economy)
    {
        ArgumentNullException.ThrowIfNull(economy);

        var expected = economy.OpeningCash;
        foreach (var transaction in economy.Ledger.Entries)
        {
            expected += transaction.CashDelta;
        }

        return new FinanceIntegrityResult(expected == economy.Cash, expected, economy.Cash);
    }
}

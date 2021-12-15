namespace WalletWasabi.EventSourcing.Records
{
    public record AggregateKey(string AggregateType, string AggregateId);
}

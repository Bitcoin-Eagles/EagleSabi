namespace WalletWasabi.EventSourcing.Records
{
    public record AggregateSequenceIds(long DeliveredSequenceId, long TailSequenceId);
}

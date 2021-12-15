namespace WalletWasabi.EventSourcing.Records
{
	public record UndeliveredEvent(string AggregateType, string AggregateId, WrappedEvent WrappedEvent);
}

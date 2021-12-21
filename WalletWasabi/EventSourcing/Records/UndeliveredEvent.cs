using System.Collections.Generic;

namespace WalletWasabi.EventSourcing.Records
{
	public record AggregateUndeliveredEvents(string AggregateType, string AggregateId, IReadOnlyList<WrappedEvent> WrappedEvents)
		: AggregateKey(AggregateType, AggregateId);
}

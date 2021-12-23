using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing.Records
{
	public record WrappedEvent<TEvent>(
		string AggregateType,
		string AggregateId,
		long SequenceId,
		TEvent DomainEvent,
		Guid SourceId)
		: WrappedEvent(AggregateType, AggregateId, SequenceId, DomainEvent, SourceId)
		where TEvent : IEvent
	{
		public new TEvent DomainEvent { get; init; } = DomainEvent;
	}

	public abstract record WrappedEvent(
		string AggregateType,
		string AggregateId,
		long SequenceId,
		IEvent DomainEvent,
		Guid SourceId)
	{
		public static WrappedEvent CreateDynamic(
			string aggregateType,
			string aggregateId,
			long sequenceId,
			IEvent domainEvent,
			Guid sourceId)
		{
			return Create(aggregateType, aggregateId, sequenceId, (dynamic)domainEvent, sourceId);
		}

		protected static WrappedEvent<TEvent> Create<TEvent>(
			string aggregateType,
			string aggregateId,
			long sequenceId,
			TEvent domainEvent,
			Guid sourceId) where TEvent : IEvent
		{
			return new WrappedEvent<TEvent>(aggregateType, aggregateId, sequenceId, domainEvent, sourceId);
		}
	}
}

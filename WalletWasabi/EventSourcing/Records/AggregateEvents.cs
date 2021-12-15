using System.Collections.Immutable;

namespace WalletWasabi.EventSourcing.Records
{
    public record AggregateEvents(long TailSequenceId, ImmutableList<WrappedEvent> Events, Guid TransactionId)
    {
        /// <summary>
        /// SequenceId of the last event of this aggregate
        /// </summary>
        public long TailSequenceId { get; init; } = TailSequenceId;

        /// <summary>
        /// Ordered list of events
        /// </summary>
        public ImmutableList<WrappedEvent> Events { get; init; } = Events;

        public Guid TransactionId { get; init; } = TransactionId;
    }
}

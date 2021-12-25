using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.Helpers
{
	public static class Extensions
	{
		public static IEnumerable<WrappedEvent> Wrap(
			this IEnumerable<IEvent> events,
			string aggregateType,
			string aggregateId,
			long firstSequenceId)
		{
			return events.Select(a => WrappedEvent.CreateDynamic(aggregateType, aggregateId, firstSequenceId++, a, Guid.NewGuid()));
		}

		public static Task AppendEventsAsync(
			this IEventRepository repository,
			string aggregateType,
			string aggregateId,
			IEnumerable<IEvent> events,
			long firstSequenceId = 1)
		{
			return repository.AppendEventsAsync(aggregateType, aggregateId, events.Wrap(aggregateType, aggregateId, firstSequenceId));
		}
	}
}

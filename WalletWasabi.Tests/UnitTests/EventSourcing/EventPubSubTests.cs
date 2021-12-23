using Shouldly;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.EventSourcing
{
	public class EventPubSubTests
	{
		protected IEventPubSub EventPubSub { get; init; }
		protected IEventStore EventStore { get; init; }

		public EventPubSubTests()
		{
			var eventRepository = new InMemoryEventRepository();
			EventPubSub = new EventPubSub(eventRepository, new PubSub());
			EventStore = new TestEventStore(
				eventRepository,
				new TestDomainAggregateFactory(),
				new TestDomainCommandProcessorFactory(),
				EventPubSub);
		}

		[Fact]
		public async Task Receive_Async()
		{
			// Arrange
			var command = new StartRound(1000, Guid.NewGuid());
			var receivedWrappedEvents = new List<WrappedEvent>();
			await EventPubSub.SubscribeAsync(new Subscriber<WrappedEvent<RoundStarted>>(a => receivedWrappedEvents.Add(a)));

			// Act
			var result = await EventStore.ProcessCommandAsync(command, nameof(TestRoundAggregate), "1");

			// Assert
			receivedWrappedEvents.ShouldBe(result.NewEvents);
		}
	}
}

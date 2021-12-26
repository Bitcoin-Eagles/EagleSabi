using Shouldly;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.EventSourcing
{
	public class EventPubSubTests
	{
		protected IEventRepository EventRepository { get; init; }
		protected IEventPubSub EventPubSub { get; init; }
		protected IEventStore EventStore { get; init; }

		public EventPubSubTests()
		{
			EventRepository = new InMemoryEventRepository();
			EventPubSub = new EventPubSub(EventRepository, new PubSub());
			EventStore = new TestEventStore(
				EventRepository,
				new TestDomainAggregateFactory(),
				new TestDomainCommandProcessorFactory(),
				EventPubSub);
		}

		[Fact]
		public async Task Receive_Async()
		{
			// Arrange
			var command = new StartRound(1000, Guid.NewGuid());
			var receivedEvents = new List<WrappedEvent>();
			await EventPubSub.SubscribeAsync(new Subscriber<WrappedEvent<RoundStarted>>(a => receivedEvents.Add(a)));

			// Act
			var result = await EventStore.ProcessCommandAsync(command, nameof(TestRoundAggregate), "1");

			// Assert
			receivedEvents.ShouldBe(result.NewEvents);
		}

		[Fact]
		public async Task Receive_TwoSubscribers_Exceptions_Async()
		{
			// Arrange
			var command = new StartRound(1000, Guid.NewGuid());
			var receivedEvents1 = new List<WrappedEvent>();
			var receivedEvents2 = new List<WrappedEvent>();
			await EventPubSub.SubscribeAsync(new Subscriber<WrappedEvent<RoundStarted>>(a =>
			{
				receivedEvents1.Add(a);
				throw new TestException("1");
			}));
			await EventPubSub.SubscribeAsync(new Subscriber<WrappedEvent<RoundStarted>>(a =>
			{
				receivedEvents2.Add(a);
				throw new TestException("2");
			}));

			// Act
			async Task Act()
			{
				await EventStore.ProcessCommandAsync(command!, nameof(TestRoundAggregate), "1");
			}

			// Assert
			var exception = await Assert.ThrowsAsync<AggregateException>(Act);
			var appendedEvents = await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "1");
			receivedEvents1.ShouldBe(appendedEvents);
			receivedEvents2.ShouldBe(appendedEvents);
		}
	}
}

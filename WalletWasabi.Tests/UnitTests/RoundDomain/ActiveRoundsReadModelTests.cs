using Microsoft.Extensions.Hosting;
using Shouldly;
using System.Threading.Tasks;
using WalletWasabi.Domains.Arena.Aggregates;
using WalletWasabi.Domains.Arena.Events;
using WalletWasabi.Domains.Arena.ReadModels;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.Interfaces;
using WalletWasabi.Services;
using WalletWasabi.Tests.UnitTests.EventSourcing.Helpers;
using WalletWasabi.WabiSabi.Backend.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.RoundDomain
{
	public class ActiveRoundsReadModelTests : IAsyncLifetime, IDisposable
	{
		private const string ID_1 = "ID_1";
		private const string ID_2 = "ID_2";

		protected IBackgroundTaskQueue BackgroundTaskQueue { get; init; }
		protected IHostedService QueuedHostedService { get; init; }
		protected IEventRepository EventRepository { get; init; }
		protected IPubSub PubSub { get; init; }
		protected IEventPubSub EventPubSub { get; init; }
		protected ActiveRoundsReadModel ActiveRounds { get; init; }

		public ActiveRoundsReadModelTests()
		{
			BackgroundTaskQueue = new BackgroundTaskQueue();
			QueuedHostedService = new QueuedHostedService(BackgroundTaskQueue, null!);
			EventRepository = new InMemoryEventRepository();
			PubSub = new PubSub();
			EventPubSub = new EventPubSub(EventRepository, PubSub, BackgroundTaskQueue);
			ActiveRounds = new(EventPubSub);
		}

		public async Task InitializeAsync()
		{
			await ActiveRounds.Start();
			await QueuedHostedService.StartAsync(default);
		}

		[Fact]
		public void Rounds_Empty_Async()
		{
			// Arrange

			// Act

			// Assert
			ActiveRounds.Rounds.Count.ShouldBe(0);
		}

		[Fact]
		public async Task Rounds_Single_InputRegistration_Async()
		{
			// Arrange
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_1, new IEvent[]
			{
				new RoundStartedEvent(null!),
			});

			// Act
			await EventPubSub.PublishAllAsync(default);

			// Assert
			ActiveRounds.Rounds.Count.ShouldBe(1);
			ActiveRounds.Rounds.ContainsKey(ID_1).ShouldBeTrue();
			ActiveRounds.Rounds[ID_1].Phase.ShouldBe(Phase.InputRegistration);
			ActiveRounds.Rounds[ID_1].BlameOf.ShouldBeNull();
		}

		[Fact]
		public async Task Rounds_Single_InputRegistration_BlameOf_Async()
		{
			// Arrange
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_1, new IEvent[]
			{
				new RoundStartedEvent(new RoundParameters2(default!,default!,default!,default,default,default,default,default,default,default,default,default!){
					BlameOf= NBitcoin.uint256.One}),
			});

			// Act
			await EventPubSub.PublishAllAsync(default);

			// Assert
			ActiveRounds.Rounds.Count.ShouldBe(1);
			ActiveRounds.Rounds.ContainsKey(ID_1).ShouldBeTrue();
			ActiveRounds.Rounds[ID_1].Phase.ShouldBe(Phase.InputRegistration);
			ActiveRounds.Rounds[ID_1].BlameOf.ShouldBe(NBitcoin.uint256.One);
		}

		[Fact]
		public async Task Rounds_Single_ConnectionConfirmation_Async()
		{
			// Arrange
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_1, new IEvent[]
			{
				new RoundStartedEvent(null!),
				new InputsConnectionConfirmationStartedEvent(),
			});

			// Act
			await EventPubSub.PublishAllAsync(default);

			// Assert
			ActiveRounds.Rounds.Count.ShouldBe(1);
			ActiveRounds.Rounds.ContainsKey(ID_1).ShouldBeTrue();
			ActiveRounds.Rounds[ID_1].Phase.ShouldBe(Phase.ConnectionConfirmation);
		}

		[Fact]
		public async Task Rounds_Single_OutputRegistration_Async()
		{
			// Arrange
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_1, new IEvent[]
			{
				new RoundStartedEvent(null!),
				new InputsConnectionConfirmationStartedEvent(),
				new OutputRegistrationStartedEvent(),
			});

			// Act
			await EventPubSub.PublishAllAsync(default);

			// Assert
			ActiveRounds.Rounds.Count.ShouldBe(1);
			ActiveRounds.Rounds.ContainsKey(ID_1).ShouldBeTrue();
			ActiveRounds.Rounds[ID_1].Phase.ShouldBe(Phase.OutputRegistration);
		}

		[Fact]
		public async Task Rounds_Single_TransactionSigning_Async()
		{
			// Arrange
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_1, new IEvent[]
			{
				new RoundStartedEvent(null!),
				new InputsConnectionConfirmationStartedEvent(),
				new OutputRegistrationStartedEvent(),
				new SigningStartedEvent(),
			});

			// Act
			await EventPubSub.PublishAllAsync(default);

			// Assert
			ActiveRounds.Rounds.Count.ShouldBe(1);
			ActiveRounds.Rounds.ContainsKey(ID_1).ShouldBeTrue();
			ActiveRounds.Rounds[ID_1].Phase.ShouldBe(Phase.TransactionSigning);
		}

		[Fact]
		public async Task Rounds_Single_Ended_Async()
		{
			// Arrange
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_1, new IEvent[]
			{
				new RoundStartedEvent(null!),
				new InputsConnectionConfirmationStartedEvent(),
				new OutputRegistrationStartedEvent(),
				new SigningStartedEvent(),
				new RoundEndedEvent(),
			});

			// Act
			await EventPubSub.PublishAllAsync(default);

			// Assert
			ActiveRounds.Rounds.Count.ShouldBe(0);
		}

		[Fact]
		public async Task Rounds_Double_Started_Async()
		{
			// Arrange
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_1, new IEvent[]
			{
				new RoundStartedEvent(null!),
				new InputsConnectionConfirmationStartedEvent(),
			});
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_2, new IEvent[]
			{
				new RoundStartedEvent(null!),
				new InputsConnectionConfirmationStartedEvent(),
				new OutputRegistrationStartedEvent(),
			});

			// Act
			await EventPubSub.PublishAllAsync(default);

			// Assert
			ActiveRounds.Rounds.ContainsKey(ID_1).ShouldBeTrue();
			ActiveRounds.Rounds.ContainsKey(ID_2).ShouldBeTrue();
			ActiveRounds.Rounds[ID_1].Phase.ShouldBe(Phase.ConnectionConfirmation);
			ActiveRounds.Rounds[ID_2].Phase.ShouldBe(Phase.OutputRegistration);
		}

		[Fact]
		public async Task Rounds_Double_OneEnded_Async()
		{
			// Arrange
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_1, new IEvent[]
			{
				new RoundStartedEvent(null!),
				new InputsConnectionConfirmationStartedEvent(),
			});
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_2, new IEvent[]
			{
				new RoundStartedEvent(null!),
				new InputsConnectionConfirmationStartedEvent(),
				new OutputRegistrationStartedEvent(),
				new SigningStartedEvent(),
				new RoundEndedEvent(),
			});

			// Act
			await EventPubSub.PublishAllAsync(default);

			// Assert
			ActiveRounds.Rounds.ContainsKey(ID_1).ShouldBeTrue();
			ActiveRounds.Rounds.ContainsKey(ID_2).ShouldBeFalse();
			ActiveRounds.Rounds[ID_1].Phase.ShouldBe(Phase.ConnectionConfirmation);
		}

		[Fact]
		public async Task Rounds_Double_BothEnded_Async()
		{
			// Arrange
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_1, new IEvent[]
			{
				new RoundStartedEvent(null!),
				new InputsConnectionConfirmationStartedEvent(),
				new OutputRegistrationStartedEvent(),
				new SigningStartedEvent(),
				new RoundEndedEvent(),
			});
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_2, new IEvent[]
			{
				new RoundStartedEvent(null!),
				new InputsConnectionConfirmationStartedEvent(),
				new OutputRegistrationStartedEvent(),
				new SigningStartedEvent(),
				new RoundEndedEvent(),
			});

			// Act
			await EventPubSub.PublishAllAsync(default);

			// Assert
			ActiveRounds.Rounds.ContainsKey(ID_1).ShouldBeFalse();
			ActiveRounds.Rounds.ContainsKey(ID_2).ShouldBeFalse();
		}

		[Fact]
		public async Task Rounds_SkipRedeliveredEvent_Async()
		{
			// Arrange
			await EventRepository.AppendEventsAsync(nameof(RoundAggregate), ID_1, new IEvent[]
			{
				new RoundStartedEvent(null!),
				new InputsConnectionConfirmationStartedEvent(),
				new OutputRegistrationStartedEvent(),
			});
			var events = await EventRepository.ListEventsAsync(nameof(RoundAggregate), ID_1);
			await EventPubSub.PublishAllAsync(default);
			var oldEvent = events[1];

			// Assume
			ActiveRounds.Rounds[ID_1].SequenceId.ShouldBeGreaterThan(oldEvent.SequenceId);

			// Act
			await PubSub.PublishDynamicAsync(oldEvent);

			// Assert
			ActiveRounds.Rounds[ID_1].SequenceId.ShouldBeGreaterThan(oldEvent.SequenceId);
			ActiveRounds.Rounds[ID_1].Phase.ShouldBe(Phase.OutputRegistration);
		}

		public Task DisposeAsync()
		{
			return QueuedHostedService.StopAsync(default);
		}

		public void Dispose()
		{
			ActiveRounds.Dispose();
		}
	}
}
using NBitcoin;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Domains.Arena.Events;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Interfaces;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Domains.Arena.ReadModels
{
	public class ActiveRoundsReadModel : IAsyncStartable, IDisposable,
		ISubscriber<WrappedEvent<RoundStartedEvent>>,
		ISubscriber<WrappedEvent<InputsConnectionConfirmationStartedEvent>>,
		ISubscriber<WrappedEvent<OutputRegistrationStartedEvent>>,
		ISubscriber<WrappedEvent<SigningStartedEvent>>,
		ISubscriber<WrappedEvent<RoundEndedEvent>>
	{
		protected SemaphoreSlim StartableSemaphore { get; init; } = new(1);

		#region Dependencies

		protected IEventPubSub EventPubSub { get; init; }

		#endregion Dependencies

		public ImmutableDictionary<string, ActiveRoundInfo> Rounds => _rounds;

		private ImmutableDictionary<string, ActiveRoundInfo> _rounds
			= ImmutableDictionary<string, ActiveRoundInfo>.Empty;

		public ActiveRoundsReadModel(IEventPubSub eventPubSub)
		{
			EventPubSub = eventPubSub;
		}

		public async Task Start()
		{
			if (await StartableSemaphore.WaitAsync(0).ConfigureAwait(false))
				await EventPubSub.SubscribeAllAsync(this).ConfigureAwait(false);
		}

		public Task Receive(WrappedEvent<RoundStartedEvent> message)
		{
			Update(message, Phase.InputRegistration, message.DomainEvent.RoundParameters?.BlameOf);
			return Task.CompletedTask;
		}

		public Task Receive(WrappedEvent<InputsConnectionConfirmationStartedEvent> message)
		{
			Update(message, Phase.ConnectionConfirmation);
			return Task.CompletedTask;
		}

		public Task Receive(WrappedEvent<OutputRegistrationStartedEvent> message)
		{
			Update(message, Phase.OutputRegistration);
			return Task.CompletedTask;
		}

		public Task Receive(WrappedEvent<SigningStartedEvent> message)
		{
			Update(message, Phase.TransactionSigning);
			return Task.CompletedTask;
		}

		public Task Receive(WrappedEvent<RoundEndedEvent> message)
		{
			ImmutableInterlocked.TryRemove(ref _rounds, message.AggregateId, out _);
			return Task.CompletedTask;
		}

		private void Update(WrappedEvent wrappedEvent, Phase phase, uint256? blameOf = null)
		{
			ImmutableInterlocked.AddOrUpdate(ref _rounds, wrappedEvent.AggregateId,
				_ => new(phase, wrappedEvent.SequenceId, blameOf ?? null),
				(a, b) =>
				{
					if (b.SequenceId < wrappedEvent.SequenceId)
					{
						var result = b with { Phase = phase, SequenceId = wrappedEvent.SequenceId };
						if (blameOf != null)
							result = result with { BlameOf = blameOf };
						return result;
					}
					else
					{
						return b;
					}
				});
		}

		public void Dispose()
		{
			StartableSemaphore.Dispose();
		}
	}
}

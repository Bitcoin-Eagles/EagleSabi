using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public class TestEventStore : EventStore, IDisposable
	{
		public TestEventStore(
			IEventRepository eventRepository,
			IAggregateFactory aggregateFactory,
			ICommandProcessorFactory commandProcessorFactory,
			IEventPubSub? eventPusher)
			: base(eventRepository, aggregateFactory, commandProcessorFactory, eventPusher)
		{
		}

		public SemaphoreSlim PreparedSemaphore { get; } = new(0);
		public SemaphoreSlim ConflictedSemaphore { get; } = new(0);
		public SemaphoreSlim AppendedSemaphore { get; } = new(0);
		public SemaphoreSlim PushedSemaphore { get; } = new(0);

		public Func<Task>? PreparedCallback { get; set; }
		public Func<Task>? ConflictedCallback { get; set; }
		public Func<Task>? AppendedCallback { get; set; }
		public Func<Task>? PushedCallback { get; set; }

		protected override async Task Prepared()
		{
			await base.Prepared();
			PreparedSemaphore.Release();
			if (PreparedCallback is not null)
				await PreparedCallback.Invoke();
		}

		protected override async Task Conflicted()
		{
			await base.Conflicted();
			ConflictedSemaphore.Release();
			if (ConflictedCallback is not null)
				await ConflictedCallback.Invoke();
		}

		protected override async Task Appended()
		{
			await base.Appended();
			AppendedSemaphore.Release();
			if (AppendedCallback is not null)
				await AppendedCallback.Invoke();
		}

		protected override async Task Pushed()
		{
			await base.Pushed();
			PushedSemaphore.Release();
			if (PushedCallback is not null)
				await PushedCallback.Invoke();
		}

		public void Dispose()
		{
			PreparedSemaphore.Dispose();
			ConflictedSemaphore.Dispose();
			AppendedSemaphore.Dispose();
			PushedSemaphore.Dispose();

			PreparedCallback = null;
			ConflictedCallback = null;
			AppendedCallback = null;
			PushedCallback = null;
		}
	}
}

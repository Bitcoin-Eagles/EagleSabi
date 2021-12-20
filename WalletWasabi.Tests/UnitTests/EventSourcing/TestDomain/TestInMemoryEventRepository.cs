using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public class TestInMemoryEventRepository : InMemoryEventRepository, IDisposable
	{
		public TestInMemoryEventRepository(ITestOutputHelper output)
		{
			Output = output;
		}

		protected ITestOutputHelper Output { get; init; }

		public SemaphoreSlim Append_Validated_Semaphore { get; } = new(0);
		public SemaphoreSlim Append_MarkedUndelivered_Semaphore { get; } = new(0);
		public SemaphoreSlim Append_Conflicted_Semaphore { get; } = new(0);
		public SemaphoreSlim Append_Appended_Semaphore { get; } = new(0);

		public SemaphoreSlim MarkUndelivered_Started_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkUndelivered_Got_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkUndelivered_UndeliveredConflictKept_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkUndelivered_Conflicted_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkUndelivered_Ended_Semaphore { get; } = new(0);

		public SemaphoreSlim MarkDelivered_Started_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkDelivered_GotAggregateEvents_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkDelivered_Conflicted_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkDelivered_Ended_Semaphore { get; } = new(0);

		public SemaphoreSlim DoMarkDelivered_Entered_Semaphore { get; } = new(0);
		public SemaphoreSlim DoMarkDelivered_Got_Semaphore { get; } = new(0);
		public SemaphoreSlim DoMarkDelivered_UndeliveredConflictFixed_Semaphore { get; } = new(0);

		public Func<Task>? Append_Validated_Callback { get; set; }
		public Func<Task>? Append_MarkedUndelivered_Callback { get; set; }
		public Func<Task>? Append_Conflicted_Callback { get; set; }
		public Func<Task>? Append_Appended_Callback { get; set; }

		public Func<Task>? MarkUndelivered_Started_Callback { get; set; }
		public Func<Task>? MarkUndelivered_Got_Callback { get; set; }
		public Func<Task>? MarkUndelivered_UndeliveredConflictKept_Callback { get; set; }
		public Func<Task>? MarkUndelivered_Conflicted_Callback { get; set; }
		public Func<Task>? MarkUndelivered_Ended_Callback { get; set; }

		public Func<Task>? MarkDelivered_Started_Callback { get; set; }
		public Func<Task>? MarkDelivered_GotAggregateEvents_Callback { get; set; }
		public Func<Task>? MarkDelivered_Conflicted_Callback { get; set; }
		public Func<Task>? MarkDelivered_Ended_Callback { get; set; }

		public Func<Task>? DoMarkDelivered_Entered_Callback { get; set; }
		public Func<Task>? DoMarkDelivered_Got_Callback { get; set; }
		public Func<Task>? DoMarkDelivered_UndeliveredConflictFixed_Callback { get; set; }

		protected override async Task Append_Validated()
		{
			await base.Append_Validated();
			Output.WriteLine(nameof(Append_Validated));
			Append_Validated_Semaphore.Release();
			if (Append_Validated_Callback is not null)
				await Append_Validated_Callback.Invoke();
		}

		protected override async Task Append_MarkedUndelivered()
		{
			await base.Append_MarkedUndelivered();
			Output.WriteLine(nameof(Append_MarkedUndelivered));
			Append_MarkedUndelivered_Semaphore.Release();
			if (Append_MarkedUndelivered_Callback is not null)
				await Append_MarkedUndelivered_Callback.Invoke();
		}

		protected override async Task Append_Conflicted()
		{
			await base.Append_Conflicted();
			Output.WriteLine(nameof(Append_Conflicted));
			Append_Conflicted_Semaphore.Release();
			if (Append_Conflicted_Callback is not null)
				await Append_Conflicted_Callback.Invoke();
		}

		protected override async Task Append_Appended()
		{
			await base.Append_Appended();
			Output.WriteLine(nameof(Append_Appended));
			Append_Appended_Semaphore.Release();
			if (Append_Appended_Callback is not null)
				await Append_Appended_Callback.Invoke();
		}

		protected override async Task MarkUndelivered_Started()
		{
			await base.MarkUndelivered_Started();
			Output.WriteLine(nameof(MarkUndelivered_Started));
			MarkUndelivered_Started_Semaphore.Release();
			if (MarkUndelivered_Started_Callback is not null)
				await MarkUndelivered_Started_Callback.Invoke();
		}

		protected override async Task MarkUndelivered_Got()
		{
			await base.MarkUndelivered_Got();
			Output.WriteLine(nameof(MarkUndelivered_Got));
			MarkUndelivered_Got_Semaphore.Release();
			if (MarkUndelivered_Got_Callback is not null)
				await MarkUndelivered_Got_Callback.Invoke();
		}

		protected override async Task MarkUndelivered_UndeliveredConflictKept()
		{
			await base.MarkUndelivered_UndeliveredConflictKept();
			Output.WriteLine(nameof(MarkUndelivered_UndeliveredConflictKept));
			MarkUndelivered_UndeliveredConflictKept_Semaphore.Release();
			if (MarkUndelivered_UndeliveredConflictKept_Callback is not null)
				await MarkUndelivered_UndeliveredConflictKept_Callback.Invoke();
		}

		protected override async Task MarkUndelivered_Conflicted()
		{
			await base.MarkUndelivered_Conflicted();
			Output.WriteLine(nameof(MarkUndelivered_Conflicted));
			MarkUndelivered_Conflicted_Semaphore.Release();
			if (MarkUndelivered_Conflicted_Callback is not null)
				await MarkUndelivered_Conflicted_Callback.Invoke();
		}

		protected override async Task MarkUndelivered_Ended()
		{
			await base.MarkUndelivered_Ended();
			Output.WriteLine(nameof(MarkUndelivered_Ended));
			MarkUndelivered_Ended_Semaphore.Release();
			if (MarkUndelivered_Ended_Callback is not null)
				await MarkUndelivered_Ended_Callback.Invoke();
		}

		protected override async Task MarkDelivered_Started()
		{
			await base.MarkDelivered_Started();
			Output.WriteLine(nameof(MarkDelivered_Started));
			MarkDelivered_Started_Semaphore.Release();
			if (MarkDelivered_Started_Callback is not null)
				await MarkDelivered_Started_Callback.Invoke();
		}

		protected override async Task MarkDelivered_GotAggregateEvents()
		{
			await base.MarkDelivered_GotAggregateEvents();
			Output.WriteLine(nameof(MarkDelivered_GotAggregateEvents));
			MarkDelivered_GotAggregateEvents_Semaphore.Release();
			if (MarkDelivered_GotAggregateEvents_Callback is not null)
				await MarkDelivered_GotAggregateEvents_Callback.Invoke();
		}

		protected override async Task MarkDelivered_Conflicted()
		{
			await base.MarkDelivered_Conflicted();
			Output.WriteLine(nameof(MarkDelivered_Conflicted));
			MarkDelivered_Conflicted_Semaphore.Release();
			if (MarkDelivered_Conflicted_Callback is not null)
				await MarkDelivered_Conflicted_Callback.Invoke();
		}

		protected override async Task MarkDelivered_Ended()
		{
			await base.MarkDelivered_Ended();
			Output.WriteLine(nameof(MarkDelivered_Ended));
			MarkDelivered_Ended_Semaphore.Release();
			if (MarkDelivered_Ended_Callback is not null)
				await MarkDelivered_Ended_Callback.Invoke();
		}

		protected override async Task DoMarkDelivered_Entered()
		{
			await base.DoMarkDelivered_Entered();
			Output.WriteLine(nameof(DoMarkDelivered_Entered));
			DoMarkDelivered_Entered_Semaphore.Release();
			if (DoMarkDelivered_Entered_Callback is not null)
				await DoMarkDelivered_Entered_Callback.Invoke();
		}

		protected override async Task DoMarkDelivered_Got()
		{
			await base.DoMarkDelivered_Got();
			Output.WriteLine(nameof(DoMarkDelivered_Got));
			DoMarkDelivered_Got_Semaphore.Release();
			if (DoMarkDelivered_Got_Callback is not null)
				await DoMarkDelivered_Got_Callback.Invoke();
		}

		protected override async Task DoMarkDelivered_UndeliveredConflictFixed()
		{
			await base.DoMarkDelivered_UndeliveredConflictFixed();
			Output.WriteLine(nameof(DoMarkDelivered_UndeliveredConflictFixed));
			DoMarkDelivered_UndeliveredConflictFixed_Semaphore.Release();
			if (DoMarkDelivered_UndeliveredConflictFixed_Callback is not null)
				await DoMarkDelivered_UndeliveredConflictFixed_Callback.Invoke();
		}

		public void Dispose()
		{
			Append_Validated_Semaphore.Dispose();
			Append_MarkedUndelivered_Semaphore.Dispose();
			Append_Conflicted_Semaphore.Dispose();
			Append_Appended_Semaphore.Dispose();

			Append_Validated_Callback = null;
			Append_MarkedUndelivered_Callback = null;
			Append_Conflicted_Callback = null;
			Append_Appended_Callback = null;

			MarkUndelivered_Started_Semaphore.Dispose();
			MarkUndelivered_Got_Semaphore.Dispose();
			MarkUndelivered_UndeliveredConflictKept_Semaphore.Dispose();
			MarkUndelivered_Conflicted_Semaphore.Dispose();
			MarkUndelivered_Ended_Semaphore.Dispose();

			MarkUndelivered_Started_Callback = null;
			MarkUndelivered_Got_Callback = null;
			MarkUndelivered_UndeliveredConflictKept_Callback = null;
			MarkUndelivered_Conflicted_Callback = null;
			MarkUndelivered_Ended_Callback = null;

			MarkDelivered_Started_Semaphore.Dispose();
			MarkDelivered_GotAggregateEvents_Semaphore.Dispose();
			MarkDelivered_Conflicted_Semaphore.Dispose();
			MarkDelivered_Ended_Semaphore.Dispose();

			MarkDelivered_Started_Callback = null;
			MarkDelivered_GotAggregateEvents_Callback = null;
			MarkDelivered_Conflicted_Callback = null;
			MarkDelivered_Ended_Callback = null;

			DoMarkDelivered_Entered_Semaphore.Dispose();
			DoMarkDelivered_Got_Semaphore.Dispose();
			DoMarkDelivered_UndeliveredConflictFixed_Semaphore.Dispose();

			DoMarkDelivered_Entered_Callback = null;
			DoMarkDelivered_Got_Callback = null;
			DoMarkDelivered_UndeliveredConflictFixed_Callback = null;
		}
	}
}

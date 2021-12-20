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

		public SemaphoreSlim Append_ValidatedSemaphore { get; } = new(0);
		public SemaphoreSlim Append_MarkedUndeliveredSemaphore { get; } = new(0);
		public SemaphoreSlim Append_ConflictedSemaphore { get; } = new(0);
		public SemaphoreSlim Append_AppendedSemaphore { get; } = new(0);

		public SemaphoreSlim MarkUndelivered_Started_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkUndelivered_Got_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkUndelivered_UndeliveredConflictKept_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkUndelivered_Conflicted_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkUndelivered_Ended_Semaphore { get; } = new(0);

		public SemaphoreSlim MarkDelivered_Started_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkDelivered_Got_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkDelivered_Conflicted_Semaphore { get; } = new(0);
		public SemaphoreSlim MarkDelivered_Ended_Semaphore { get; } = new(0);

		public SemaphoreSlim DoMarkDelivered_UndeliveredConflictFixedSemaphore { get; } = new(0);

		public Func<Task>? Append_ValidatedCallback { get; set; }
		public Func<Task>? Append_MarkedUndeliveredCallback { get; set; }
		public Func<Task>? Append_ConflictedCallback { get; set; }
		public Func<Task>? Append_AppendedCallback { get; set; }

		public Func<Task>? MarkUndelivered_Started_Callback { get; set; }
		public Func<Task>? MarkUndelivered_Got_Callback { get; set; }
		public Func<Task>? MarkUndelivered_UndeliveredConflictKept_Callback { get; set; }
		public Func<Task>? MarkUndelivered_Conflicted_Callback { get; set; }
		public Func<Task>? MarkUndelivered_Ended_Callback { get; set; }

		public Func<Task>? MarkDelivered_Started_Callback { get; set; }
		public Func<Task>? MarkDelivered_Got_Callback { get; set; }
		public Func<Task>? MarkDelivered_Conflicted_Callback { get; set; }
		public Func<Task>? MarkDelivered_Ended_Callback { get; set; }

		public Func<Task>? DoMarkDelivered_UndeliveredConflictFixedCallback { get; set; }

		protected override async Task Append_Validated()
		{
			await base.Append_Validated();
			Output.WriteLine(nameof(Append_Validated));
			Append_ValidatedSemaphore.Release();
			if (Append_ValidatedCallback is not null)
				await Append_ValidatedCallback.Invoke();
		}

		protected override async Task Append_MarkedUndelivered()
		{
			await base.Append_MarkedUndelivered();
			Output.WriteLine(nameof(Append_MarkedUndelivered));
			Append_MarkedUndeliveredSemaphore.Release();
			if (Append_MarkedUndeliveredCallback is not null)
				await Append_MarkedUndeliveredCallback.Invoke();
		}

		protected override async Task Append_Conflicted()
		{
			await base.Append_Conflicted();
			Output.WriteLine(nameof(Append_Conflicted));
			Append_ConflictedSemaphore.Release();
			if (Append_ConflictedCallback is not null)
				await Append_ConflictedCallback.Invoke();
		}

		protected override async Task Append_Appended()
		{
			await base.Append_Appended();
			Output.WriteLine(nameof(Append_Appended));
			Append_AppendedSemaphore.Release();
			if (Append_AppendedCallback is not null)
				await Append_AppendedCallback.Invoke();
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

		protected override async Task MarkDelivered_Got()
		{
			await base.MarkDelivered_Got();
			Output.WriteLine(nameof(MarkDelivered_Got));
			MarkDelivered_Got_Semaphore.Release();
			if (MarkDelivered_Got_Callback is not null)
				await MarkDelivered_Got_Callback.Invoke();
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

		protected override async Task DoMarkDelivered_UndeliveredConflictFixed()
		{
			await base.DoMarkDelivered_UndeliveredConflictFixed();
			Output.WriteLine(nameof(DoMarkDelivered_UndeliveredConflictFixed));
			DoMarkDelivered_UndeliveredConflictFixedSemaphore.Release();
			if (DoMarkDelivered_UndeliveredConflictFixedCallback is not null)
				await DoMarkDelivered_UndeliveredConflictFixedCallback.Invoke();
		}

		public void Dispose()
		{
			Append_ValidatedSemaphore.Dispose();
			Append_MarkedUndeliveredSemaphore.Dispose();
			Append_ConflictedSemaphore.Dispose();
			Append_AppendedSemaphore.Dispose();

			Append_ValidatedCallback = null;
			Append_MarkedUndeliveredCallback = null;
			Append_ConflictedCallback = null;
			Append_AppendedCallback = null;

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
			MarkDelivered_Got_Semaphore.Dispose();
			MarkDelivered_Conflicted_Semaphore.Dispose();
			MarkDelivered_Ended_Semaphore.Dispose();

			MarkDelivered_Started_Callback = null;
			MarkDelivered_Got_Callback = null;
			MarkDelivered_Conflicted_Callback = null;
			MarkDelivered_Ended_Callback = null;

			DoMarkDelivered_UndeliveredConflictFixedSemaphore.Dispose();

			DoMarkDelivered_UndeliveredConflictFixedCallback = null;
		}
	}
}

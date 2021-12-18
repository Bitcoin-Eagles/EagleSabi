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

		public SemaphoreSlim DoMarkDelivered_UndeliveredConflictFixedSemaphore { get; } = new(0);

		public SemaphoreSlim TryFixUndelivered_DetectedSemaphore { get; } = new(0);
		public SemaphoreSlim TryFixUndelivered_UpdatedSemaphore { get; } = new(0);
		public SemaphoreSlim TryFixUndelivered_UpdateConflictedSemaphore { get; } = new(0);
		public SemaphoreSlim TryFixUndelivered_RemovedSemaphore { get; } = new(0);
		public SemaphoreSlim TryFixUndelivered_RemoveConflictedSemaphore { get; } = new(0);

		public Func<Task>? Append_ValidatedCallback { get; set; }
		public Func<Task>? Append_MarkedUndeliveredCallback { get; set; }
		public Func<Task>? Append_ConflictedCallback { get; set; }
		public Func<Task>? Append_AppendedCallback { get; set; }

		public Func<Task>? DoMarkDelivered_UndeliveredConflictFixedCallback { get; set; }

		public Func<Task>? TryFixUndelivered_DetectedCallback { get; set; }
		public Func<Task>? TryFixUndelivered_UpdatedCallback { get; set; }
		public Func<Task>? TryFixUndelivered_UpdateConflictedCallback { get; set; }
		public Func<Task>? TryFixUndelivered_RemovedCallback { get; set; }
		public Func<Task>? TryFixUndelivered_RemoveConflictedCallback { get; set; }

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

		protected override async Task DoMarkDelivered_UndeliveredConflictFixed()
		{
			await base.DoMarkDelivered_UndeliveredConflictFixed();
			Output.WriteLine(nameof(DoMarkDelivered_UndeliveredConflictFixed));
			DoMarkDelivered_UndeliveredConflictFixedSemaphore.Release();
			if (DoMarkDelivered_UndeliveredConflictFixedCallback is not null)
				await DoMarkDelivered_UndeliveredConflictFixedCallback.Invoke();
		}

		protected override async Task TryFixUndelivered_Detected()
		{
			await base.TryFixUndelivered_Detected();
			Output.WriteLine(nameof(TryFixUndelivered_Detected));
			TryFixUndelivered_DetectedSemaphore.Release();
			if (TryFixUndelivered_DetectedCallback is not null)
				await TryFixUndelivered_DetectedCallback.Invoke();
		}

		protected override async Task TryFixUndelivered_Updated()
		{
			await base.TryFixUndelivered_Updated();
			Output.WriteLine(nameof(TryFixUndelivered_Updated));
			TryFixUndelivered_UpdatedSemaphore.Release();
			if (TryFixUndelivered_UpdatedCallback is not null)
				await TryFixUndelivered_UpdatedCallback.Invoke();
		}

		protected override async Task TryFixUndelivered_UpdateConflicted()
		{
			await base.TryFixUndelivered_UpdateConflicted();
			Output.WriteLine(nameof(TryFixUndelivered_UpdateConflicted));
			TryFixUndelivered_UpdateConflictedSemaphore.Release();
			if (TryFixUndelivered_UpdateConflictedCallback is not null)
				await TryFixUndelivered_UpdateConflictedCallback.Invoke();
		}

		protected override async Task TryFixUndelivered_Removed()
		{
			await base.TryFixUndelivered_Removed();
			Output.WriteLine(nameof(TryFixUndelivered_Removed));
			TryFixUndelivered_RemovedSemaphore.Release();
			if (TryFixUndelivered_RemovedCallback is not null)
				await TryFixUndelivered_RemovedCallback.Invoke();
		}

		protected override async Task TryFixUndelivered_RemoveConflicted()
		{
			await base.TryFixUndelivered_RemoveConflicted();
			Output.WriteLine(nameof(TryFixUndelivered_RemoveConflicted));
			TryFixUndelivered_RemoveConflictedSemaphore.Release();
			if (TryFixUndelivered_RemoveConflictedCallback is not null)
				await TryFixUndelivered_RemoveConflictedCallback.Invoke();
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

			DoMarkDelivered_UndeliveredConflictFixedSemaphore.Dispose();

			DoMarkDelivered_UndeliveredConflictFixedCallback = null;

			TryFixUndelivered_DetectedSemaphore.Dispose();
			TryFixUndelivered_UpdatedSemaphore.Dispose();
			TryFixUndelivered_UpdateConflictedSemaphore.Dispose();
			TryFixUndelivered_RemovedSemaphore.Dispose();
			TryFixUndelivered_RemoveConflictedSemaphore.Dispose();

			TryFixUndelivered_DetectedCallback = null;
			TryFixUndelivered_UpdatedCallback = null;
			TryFixUndelivered_UpdateConflictedCallback = null;
			TryFixUndelivered_RemovedCallback = null;
			TryFixUndelivered_RemoveConflictedCallback = null;
		}
	}
}

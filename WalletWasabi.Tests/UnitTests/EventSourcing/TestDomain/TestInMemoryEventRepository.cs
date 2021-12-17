using System.Threading;
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

		public SemaphoreSlim TryFixUndelivered_UpdatedSemaphore { get; } = new(0);
		public SemaphoreSlim TryFixUndelivered_UpdateConflictedSemaphore { get; } = new(0);
		public SemaphoreSlim TryFixUndelivered_RemovedSemaphore { get; } = new(0);
		public SemaphoreSlim TryFixUndelivered_RemoveConflictedSemaphore { get; } = new(0);

		public Action? Append_ValidatedCallback { get; set; }
		public Action? Append_MarkedUndeliveredCallback { get; set; }
		public Action? Append_ConflictedCallback { get; set; }
		public Action? Append_AppendedCallback { get; set; }

		public Action? DoMarkDelivered_UndeliveredConflictFixedCallback { get; set; }

		public Action? TryFixUndelivered_UpdatedCallback { get; set; }
		public Action? TryFixUndelivered_UpdateConflictedCallback { get; set; }
		public Action? TryFixUndelivered_RemovedCallback { get; set; }
		public Action? TryFixUndelivered_RemoveConflictedCallback { get; set; }

		protected override void Append_Validated()
		{
			base.Append_Validated();
			Output.WriteLine(nameof(Append_Validated));
			Append_ValidatedSemaphore.Release();
			Append_ValidatedCallback?.Invoke();
		}

		protected override void Append_MarkedUndelivered()
		{
			base.Append_MarkedUndelivered();
			Output.WriteLine(nameof(Append_MarkedUndelivered));
			Append_MarkedUndeliveredSemaphore.Release();
			Append_MarkedUndeliveredCallback?.Invoke();
		}

		protected override void Append_Conflicted()
		{
			base.Append_Conflicted();
			Output.WriteLine(nameof(Append_Conflicted));
			Append_ConflictedSemaphore.Release();
			Append_ConflictedCallback?.Invoke();
		}

		protected override void Append_Appended()
		{
			base.Append_Appended();
			Output.WriteLine(nameof(Append_Appended));
			Append_AppendedSemaphore.Release();
			Append_AppendedCallback?.Invoke();
		}

		protected override void DoMarkDelivered_UndeliveredConflictFixed()
		{
			base.DoMarkDelivered_UndeliveredConflictFixed();
			Output.WriteLine(nameof(DoMarkDelivered_UndeliveredConflictFixed));
			DoMarkDelivered_UndeliveredConflictFixedSemaphore.Release();
			DoMarkDelivered_UndeliveredConflictFixedCallback?.Invoke();
		}

		protected override void TryFixUndelivered_Updated()
		{
			base.TryFixUndelivered_Updated();
			Output.WriteLine(nameof(TryFixUndelivered_Updated));
			TryFixUndelivered_UpdatedSemaphore.Release();
			TryFixUndelivered_UpdatedCallback?.Invoke();
		}

		protected override void TryFixUndelivered_UpdateConflicted()
		{
			base.TryFixUndelivered_UpdateConflicted();
			Output.WriteLine(nameof(TryFixUndelivered_UpdateConflicted));
			TryFixUndelivered_UpdateConflictedSemaphore.Release();
			TryFixUndelivered_UpdateConflictedCallback?.Invoke();
		}

		protected override void TryFixUndelivered_Removed()
		{
			base.TryFixUndelivered_Removed();
			Output.WriteLine(nameof(TryFixUndelivered_Removed));
			TryFixUndelivered_RemovedSemaphore.Release();
			TryFixUndelivered_RemovedCallback?.Invoke();
		}

		protected override void TryFixUndelivered_RemoveConflicted()
		{
			base.TryFixUndelivered_RemoveConflicted();
			Output.WriteLine(nameof(TryFixUndelivered_RemoveConflicted));
			TryFixUndelivered_RemoveConflictedSemaphore.Release();
			TryFixUndelivered_RemoveConflictedCallback?.Invoke();
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

			TryFixUndelivered_UpdatedSemaphore.Dispose();
			TryFixUndelivered_UpdateConflictedSemaphore.Dispose();
			TryFixUndelivered_RemovedSemaphore.Dispose();
			TryFixUndelivered_RemoveConflictedSemaphore.Dispose();

			TryFixUndelivered_UpdatedCallback = null;
			TryFixUndelivered_UpdateConflictedCallback = null;
			TryFixUndelivered_RemovedCallback = null;
			TryFixUndelivered_RemoveConflictedCallback = null;
		}
	}
}

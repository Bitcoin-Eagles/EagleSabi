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

		public Action? Append_ValidatedCallback { get; set; }
		public Action? Append_MarkedUndeliveredCallback { get; set; }
		public Action? Append_ConflictedCallback { get; set; }
		public Action? Append_AppendedCallback { get; set; }

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

		public void Dispose()
		{
			Append_ValidatedSemaphore.Dispose();
			Append_MarkedUndeliveredSemaphore.Dispose();
			Append_ConflictedSemaphore.Dispose();
			Append_AppendedSemaphore.Dispose();
		}
	}
}

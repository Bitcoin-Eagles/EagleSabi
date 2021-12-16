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

		public SemaphoreSlim ValidatedSemaphore { get; } = new(0);
		public SemaphoreSlim ConflictedSemaphore { get; } = new(0);
		public SemaphoreSlim AppendedSemaphore { get; } = new(0);

		public Action? ValidatedCallback { get; set; }
		public Action? ConflictedCallback { get; set; }
		public Action? AppendedCallback { get; set; }

		protected override void Append_Validated()
		{
			base.Append_Validated();
			Output.WriteLine(nameof(Append_Validated));
			ValidatedSemaphore.Release();
			ValidatedCallback?.Invoke();
		}

		protected override void Append_Conflicted()
		{
			base.Append_Conflicted();
			Output.WriteLine(nameof(Append_Conflicted));
			ConflictedSemaphore.Release();
			ConflictedCallback?.Invoke();
		}

		protected override void Append_Appended()
		{
			base.Append_Appended();
			Output.WriteLine(nameof(Append_Appended));
			AppendedSemaphore.Release();
			AppendedCallback?.Invoke();
		}

		public void Dispose()
		{
			ValidatedSemaphore.Dispose();
			ConflictedSemaphore.Dispose();
			AppendedSemaphore.Dispose();
		}
	}
}

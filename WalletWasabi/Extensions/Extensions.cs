using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Extensions
{
	public static class Extensions
	{
		/// <summary>
		/// Waits until all previously enqueued tasks have been finished. Note: Queue doesn't necesarilly has to be empty afterwards.
		/// </summary>
		public static async Task WaitAsync(this IBackgroundTaskQueue queue, CancellationToken cancellationToken = default)
		{
			using var processed = new SemaphoreSlim(0);
			var enqueue = queue.QueueBackgroundWorkItemAsync(_ => { processed.Release(); return ValueTask.CompletedTask; });
			await Task.WhenAny(enqueue.AsTask(), Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
			await processed.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
	}
}

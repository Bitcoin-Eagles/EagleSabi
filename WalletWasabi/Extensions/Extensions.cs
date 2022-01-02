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
			var processed = new TaskCompletionSource();
			var enqueue = queue.QueueBackgroundWorkItemAsync(_ => { processed.SetResult(); return ValueTask.CompletedTask; });
			await enqueue.AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
			await processed.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
	}
}

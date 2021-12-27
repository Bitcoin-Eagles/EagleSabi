using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Services
{
	// source: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-6.0&tabs=visual-studio#queued-background-tasks
	public class BackgroundTaskQueue : IBackgroundTaskQueue
	{
		private readonly Channel<Func<CancellationToken, ValueTask>> _queue;

#warning TODO:

		// TODO: put capacity into config
		public BackgroundTaskQueue(int capacity = 1_000)
		{
			// Capacity should be set based on the expected application load and
			// number of concurrent threads accessing the queue.
			// BoundedChannelFullMode.Wait will cause calls to WriteAsync() to return a task,
			// which completes only when space became available. This leads to backpressure,
			// in case too many publishers/calls start accumulating.
			var options = new BoundedChannelOptions(capacity)
			{
				FullMode = BoundedChannelFullMode.Wait
			};
			_queue = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(options);
		}

		public async ValueTask QueueBackgroundWorkItemAsync(
			Func<CancellationToken, ValueTask> workItem)
		{
			if (workItem == null)
				throw new ArgumentNullException(nameof(workItem));

			await _queue.Writer.WriteAsync(workItem).ConfigureAwait(false);
		}

		public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(
			CancellationToken cancellationToken)
		{
			var workItem = await _queue.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);

			return workItem;
		}
	}
}

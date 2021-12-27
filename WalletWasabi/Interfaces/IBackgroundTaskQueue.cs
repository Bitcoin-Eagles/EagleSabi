using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Interfaces
{
	// source: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-6.0&tabs=visual-studio#queued-background-tasks
	public interface IBackgroundTaskQueue
	{
		ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, ValueTask> workItem);

		ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(
			CancellationToken cancellationToken);
	}
}

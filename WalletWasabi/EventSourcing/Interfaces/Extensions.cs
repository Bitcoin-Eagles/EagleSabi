using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Helpers;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public static class Extensions
	{
		public static async Task PublishDynamicAsync(this IPubSub pubSub, dynamic message)
		{
			await pubSub.PublishAsync(message).ConfigureAwait(false);
		}

		public static async Task SubscribeAllAsync(this IEventPubSub eventPubSub, object subscriber)
		{
			Guard.NotNull(nameof(eventPubSub), eventPubSub);
			Guard.NotNull(nameof(subscriber), subscriber);
			var interfaces = subscriber.GetType().GetInterfaces();
			foreach (var @interface in interfaces)
			{
				Type topic;
				if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(ISubscriber<>)
					&& (topic = @interface.GetGenericArguments()[0]).IsGenericType
						&& topic.GetGenericTypeDefinition() == typeof(WrappedEvent<>))
				{
					var domainEventType = topic.GetGenericArguments()[0];
					var subscribeTask = (Task)eventPubSub.GetType().GetMethod(nameof(IEventPubSub.SubscribeAsync))!
						.MakeGenericMethod(domainEventType).Invoke(eventPubSub, new[] { subscriber })!;
					await subscribeTask.ConfigureAwait(false);
				}
			}
		}
	}
}

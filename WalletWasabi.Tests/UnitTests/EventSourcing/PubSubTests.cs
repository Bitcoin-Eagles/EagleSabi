using Shouldly;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain.Messages;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.EventSourcing
{
	public class PubSubTests
	{
		protected IPubSub PubSub { get; init; }

		public PubSubTests()
		{
			PubSub = new PubSub();
		}

		[Fact]
		public async Task Receive_Async()
		{
			// Arrange
			DogMessage? dog = null;
			await PubSub.SubscribeAsync(Subscriber.Create<DogMessage>(a => dog = a));

			// Act
			await PubSub.PublishAsync(new DogMessage());

			// Assert
			dog.ShouldNotBeNull();
		}
	}
}

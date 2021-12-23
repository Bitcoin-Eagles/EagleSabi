using Shouldly;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.EventSourcing.Dummy.Messages;
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

		[Fact]
		public async Task Receive_Exception_Async()
		{
			// Arrange
			DogMessage? dog = null;
			await PubSub.SubscribeAsync(Subscriber.Create<DogMessage>(a => { dog = a; throw new TestException("a"); }));

			// Act
			async Task Act()
			{
				await PubSub.PublishAsync(new DogMessage());
			}

			// Assert
			await Assert.ThrowsAsync<TestException>(Act);
			dog.ShouldNotBeNull();
		}

		[Fact]
		public async Task Receive_TwoSubscribers_Async()
		{
			// Arrange
			DogMessage? dog1 = null;
			DogMessage? dog2 = null;
			await PubSub.SubscribeAsync(Subscriber.Create<DogMessage>(a => dog1 = a));
			await PubSub.SubscribeAsync(Subscriber.Create<DogMessage>(a => dog2 = a));

			// Act
			await PubSub.PublishAsync(new DogMessage());

			// Assert
			dog1.ShouldNotBeNull();
			dog2.ShouldNotBeNull();
		}

		[Fact]
		public async Task Receive_TwoSubscribers_AggregateExceptions_Async()
		{
			// Arrange
			DogMessage? dog1 = null;
			DogMessage? dog2 = null;
			await PubSub.SubscribeAsync(Subscriber.Create<DogMessage>(a =>
			{
				dog1 = a;
				throw new TestException(nameof(dog1));
			}));
			await PubSub.SubscribeAsync(Subscriber.Create<DogMessage>(a =>
			{
				dog2 = a;
				throw new TestException(nameof(dog2));
			}));

			// Act
			async Task Act()
			{
				await PubSub.PublishAsync(new DogMessage());
			}

			// Assert
			var exception = await Assert.ThrowsAsync<AggregateException>(Act);
			exception.InnerExceptions.Count.ShouldBe(2);
			exception.InnerExceptions.ShouldContain(a => a.Message == nameof(dog1));
			exception.InnerExceptions.ShouldContain(a => a.Message == nameof(dog2));
			dog1.ShouldNotBeNull();
			dog2.ShouldNotBeNull();
		}

		[Fact]
		public async Task Receive_NothingBaseClass_Async()
		{
			// Arrage
			BaseMessage? baseMessage = null;
			DogMessage? dog = null;
			await PubSub.SubscribeAsync(Subscriber.Create<BaseMessage>(a => baseMessage = a));
			await PubSub.SubscribeAsync(Subscriber.Create<DogMessage>(a => dog = a));

			// Act
			await PubSub.PublishAsync(new DogMessage());

			// Assert
			typeof(DogMessage).IsAssignableTo(typeof(BaseMessage)).ShouldBeTrue();
			baseMessage.ShouldBeNull();
			dog.ShouldNotBeNull();
		}

		[Fact]
		public async Task Receive_BothBaseClassExplicitly_Async()
		{
			// Arrage
			BaseMessage? baseMessage = null;
			MamalMessage? mamal = null;
			DogMessage? dog = null;
			await PubSub.SubscribeAsync(Subscriber.Create<BaseMessage>(a => baseMessage = a));
			await PubSub.SubscribeAsync(Subscriber.Create<MamalMessage>(a => baseMessage = a));
			await PubSub.SubscribeAsync(Subscriber.Create<DogMessage>(a => dog = a));

			// Act
			var message = new DogMessage();
			await PubSub.PublishAsync(message);
			await PubSub.PublishAsync<BaseMessage>(message);

			// Assert
			typeof(DogMessage).IsAssignableTo(typeof(BaseMessage)).ShouldBeTrue();
			typeof(DogMessage).IsAssignableTo(typeof(MamalMessage)).ShouldBeTrue();
			typeof(MamalMessage).IsAssignableTo(typeof(BaseMessage)).ShouldBeTrue();
			baseMessage.ShouldNotBeNull();
			dog.ShouldNotBeNull();
			mamal.ShouldBeNull();
		}
	}
}

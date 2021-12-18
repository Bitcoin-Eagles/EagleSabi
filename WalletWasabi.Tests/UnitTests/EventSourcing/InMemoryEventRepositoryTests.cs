using Shouldly;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Exceptions;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain;
using Xunit;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests.EventSourcing
{
	public class InMemoryEventRepositoryTests : IDisposable
	{
		private readonly TimeSpan _semaphoreWaitTimeout = TimeSpan.FromSeconds(5);

		public InMemoryEventRepositoryTests(ITestOutputHelper output)
		{
			TestEventRepository = new TestInMemoryEventRepository(output);
			EventRepository = TestEventRepository;
		}

		private IEventRepository EventRepository { get; init; }
		private TestInMemoryEventRepository TestEventRepository { get; init; }

		[Fact]
		public async Task AppendEvents_Zero_Async()
		{
			// Arrange
			var events = Array.Empty<WrappedEvent>();

			// Act
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events);

			// Assert
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "MY_ID_1"))
				.SequenceEqual(events));
			Assert.DoesNotContain(await EventRepository.ListUndeliveredEventsAsync(),
				a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == "MY_ID_1");
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(Array.Empty<string>()));
		}

		[Fact]
		public async Task AppendEvents_One_Async()
		{
			// Arrange
			var events = new[]
			{
				new TestWrappedEvent(1),
			};

			// Act
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events);

			// Assert
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "MY_ID_1"))
				.SequenceEqual(events));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == "MY_ID_1")
				.WrappedEvents
				.SequenceEqual(events));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "MY_ID_1" }));
		}

		[Fact]
		public async Task AppendEvents_Two_Async()
		{
			// Arrange
			var events = new[]
			{
				new TestWrappedEvent(1),
				new TestWrappedEvent(2),
			};

			// Act
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events);

			// Assert
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "MY_ID_1"))
				.SequenceEqual(events));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == "MY_ID_1")
				.WrappedEvents
				.SequenceEqual(events));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "MY_ID_1" }));
		}

		[Fact]
		public async Task AppendEvents_NegativeSequenceId_Async()
		{
			// Arrange
			var events = new[]
			{
				new TestWrappedEvent(-1)
			};

			// Act
			async Task ActionAsync()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events).ConfigureAwait(false);
			}

			// Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(ActionAsync);
			Assert.Contains("First event sequenceId is not natural number", ex.Message);
		}

		[Fact]
		public async Task AppendEvents_SkippedSequenceId_Async()
		{
			// Arrange
			var events = new[]
			{
				new TestWrappedEvent(2)
			};

			// Act
			async Task ActionAsync()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events);
			}

			// Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(ActionAsync);
			Assert.Contains(
				"Invalid firstSequenceId (gap in sequence ids) expected: '1' given: '2'",
				ex.Message);
		}

		[Fact]
		public async Task AppendEvents_OptimisticConcurrency_Async()
		{
			// Arrange
			var events = new[]
			{
				new TestWrappedEvent(1)
			};
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events);

			// Act
			async Task ActionAsync()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events);
			}

			// Assert
			var ex = await Assert.ThrowsAsync<OptimisticConcurrencyException>(ActionAsync);
			Assert.Contains("Conflict", ex.Message);
		}

		[Fact]
		public async Task AppendEventsAsync_Interleaving_Async()
		{
			// Arrange
			var events_a_0 = new[] { new TestWrappedEvent(1) };
			var events_b_0 = new[] { new TestWrappedEvent(1) };
			var events_a_1 = new[] { new TestWrappedEvent(2) };
			var events_b_1 = new[] { new TestWrappedEvent(2) };

			// Act
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "a", events_a_0);
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "b", events_b_0);
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "a", events_a_1);
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "b", events_b_1);

			// Assert
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "a"))
				.SequenceEqual(events_a_0.Concat(events_a_1)));
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "b"))
				.SequenceEqual(events_b_0.Concat(events_b_1)));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == "a")
				.WrappedEvents
				.SequenceEqual(events_a_0.Concat(events_a_1)));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == "b")
				.WrappedEvents
				.SequenceEqual(events_b_0.Concat(events_b_1)));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "a", "b" }));
		}

		[Fact]
		public async Task AppendEventsAsync_InterleavingConflict_Async()
		{
			// Arrange
			var events_a_0 = new[] { new TestWrappedEvent(1) };
			var events_b_0 = new[] { new TestWrappedEvent(1) };
			var events_a_1 = new[] { new TestWrappedEvent(2) };

			// Act
			async Task ActionAsync()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "a", events_a_0);
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "b", events_b_0);
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "a", events_a_1);
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "a", events_a_1);
			}

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(ActionAsync);
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "a"))
				.SequenceEqual(events_a_0.Concat(events_a_1)));
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "b"))
				.SequenceEqual(events_b_0));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == "a")
				.WrappedEvents
				.SequenceEqual(events_a_0.Concat(events_a_1)));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == "b")
				.WrappedEvents
				.SequenceEqual(events_b_0));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "a", "b" }));
		}

		[Fact]
		public async Task AppendEvents_AppendIsAtomic_Async()
		{
			// Arrange
			var events1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
			var events2 = new[] { new TestWrappedEvent(2, "b"), new TestWrappedEvent(3, "b") };

			// Act
			async Task ActionAsync()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events1);
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events2);
			}

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(ActionAsync);
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "MY_ID_1"))
				.Cast<TestWrappedEvent>().SequenceEqual(events1));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == "MY_ID_1")
				.WrappedEvents.Cast<TestWrappedEvent>().SequenceEqual(events1));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "MY_ID_1" }));
		}

#if DEBUG

		[Fact]
		public async Task AppendEvents_CriticalSectionConflicts_Async()
		{
			// Arrange
			var events1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
			var events2 = new[] { new TestWrappedEvent(2, "b"), new TestWrappedEvent(3, "b") };

			// Act
			async Task Append1Async()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events1);
			}
			async Task Append2Async()
			{
				Assert.True(await TestEventRepository.Append_AppendedSemaphore.WaitAsync(_semaphoreWaitTimeout));
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events2!);
			}
			async Task AppendInParallelAsync()
			{
				var task1 = Task.Run(Append1Async);
				var task2 = Task.Run(Append2Async);
				await Task.WhenAll(task1, task2);
			}
			async Task WaitForConflict()
			{
				Assert.True(await TestEventRepository.Append_ConflictedSemaphore.WaitAsync(_semaphoreWaitTimeout));
			}
			TestEventRepository.Append_AppendedCallback = WaitForConflict;

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(AppendInParallelAsync);
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "MY_ID_1"))
						.Cast<TestWrappedEvent>().SequenceEqual(events1));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == "MY_ID_1")
				.WrappedEvents.Cast<TestWrappedEvent>().SequenceEqual(events1));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "MY_ID_1" }));
		}

		[Fact]
		public async Task AppendEvents_CriticalAppendConflicts_Async()
		{
			// Arrange
			var events1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
			var events2 = new[] { new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b") };

			// Act
			async Task Append1Async()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events1);
			}
			async Task Append2Async()
			{
				Assert.True(await TestEventRepository.Append_AppendedSemaphore.WaitAsync(_semaphoreWaitTimeout));
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events2!);
			}
			async Task AppendInParallelAsync()
			{
				var task1 = Task.Run(Append1Async);
				var task2 = Task.Run(Append2Async);
				await Task.WhenAll(task1, task2);
			}
			async Task WaitForNoConflict()
			{
				Assert.False(await TestEventRepository.Append_ConflictedSemaphore.WaitAsync(_semaphoreWaitTimeout));
			}
			TestEventRepository.Append_AppendedCallback = WaitForNoConflict;

			// no conflict
			await AppendInParallelAsync();

			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "MY_ID_1"))
						.Cast<TestWrappedEvent>().SequenceEqual(events1.Concat(events2)));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == "MY_ID_1")
				.WrappedEvents.Cast<TestWrappedEvent>().SequenceEqual(events1.Concat(events2)));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { "MY_ID_1" }));
		}

		[Theory]
		[InlineData(nameof(TestInMemoryEventRepository.Append_ValidatedCallback))]
		[InlineData(nameof(TestInMemoryEventRepository.Append_AppendedCallback))]
		public async Task ListEventsAsync_ConflictWithAppending_Async(string listOnCallback)
		{
			// Arrange
			var events1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
			var events2 = new[] { new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b") };
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events1);

			// Act
			IReadOnlyList<WrappedEvent> result = ImmutableList<WrappedEvent>.Empty;
			IReadOnlyList<WrappedEvent> result2 = ImmutableList<WrappedEvent>.Empty;
			async Task ListCallback()
			{
				result = await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "MY_ID_1");
				result2 = (await EventRepository.ListUndeliveredEventsAsync())
					.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == "MY_ID_1")
					.WrappedEvents;
			}
			switch (listOnCallback)
			{
				case nameof(TestInMemoryEventRepository.Append_ValidatedCallback):
					TestEventRepository.Append_ValidatedCallback = ListCallback;
					break;

				case nameof(TestInMemoryEventRepository.Append_AppendedCallback):
					TestEventRepository.Append_AppendedCallback = ListCallback;
					break;

				default:
					throw new ApplicationException($"unexpected value listOnCallback: '{listOnCallback}'");
			}
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events2);

			// Assert
			var expected = events1.AsEnumerable();
			switch (listOnCallback)
			{
				case nameof(TestInMemoryEventRepository.Append_AppendedCallback):
					expected = expected.Concat(events2);
					break;
			}
			Assert.True(result.SequenceEqual(expected));
			Assert.True(result2.SequenceEqual(expected));
		}

#endif

		[Theory]
		[InlineData(0, 1)]
		[InlineData(0, 2)]
		[InlineData(0, 3)]
		[InlineData(0, 4)]
		[InlineData(0, 5)]
		[InlineData(1, 1)]
		[InlineData(1, 2)]
		[InlineData(1, 3)]
		[InlineData(1, 4)]
		[InlineData(2, 1)]
		[InlineData(2, 2)]
		[InlineData(2, 3)]
		[InlineData(3, 1)]
		[InlineData(3, 2)]
		[InlineData(4, 1)]
		public async Task ListEventsAsync_OptionalArguments_Async(long afterSequenceId, int limit)
		{
			// Arrange
			var events = new[]
			{
				new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a"),
				new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b")
			};
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events);

			// Act
			var result = await EventRepository.ListEventsAsync(
				nameof(TestRoundAggregate), "MY_ID_1", afterSequenceId, limit);

			// Assert
			Assert.True(result.Count <= limit);
			Assert.True(result.All(a => afterSequenceId < a.SequenceId));
		}

		[Fact]
		public async Task ListAggregateIdsAsync_Async()
		{
			// Arrange
			var events = new[]
			{
				new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a"),
				new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b")
			};
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events);
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_2", events);

			// Act
			var result = await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate));

			// Assert
			Assert.True(result.SequenceEqual(new[] { "MY_ID_1", "MY_ID_2" }));
		}

		[Theory]
		[InlineData("0", 0)]
		[InlineData("0", 1)]
		[InlineData("0", 2)]
		[InlineData("0", 3)]
		[InlineData("MY_ID_1", 0)]
		[InlineData("MY_ID_1", 1)]
		[InlineData("MY_ID_1", 2)]
		[InlineData("MY_ID_2", 0)]
		[InlineData("MY_ID_2", 1)]
		[InlineData("3", 0)]
		[InlineData("3", 1)]
		public async Task ListAggregateIdsAsync_OptionalArguments_Async(string afterAggregateId, int limit)
		{
			// Arrange
			var events = new[]
			{
				new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a"),
				new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b")
			};
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events);
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_2", events);

			// Act
			var result = await EventRepository.ListAggregateIdsAsync(
				nameof(TestRoundAggregate), afterAggregateId, limit);

			// Assert
			Assert.True(result.Count <= limit);
			Assert.True(result.All(a => afterAggregateId.CompareTo(a) <= 0));
		}

		[Theory]
		[InlineData(0, 0)]
		[InlineData(0, 1)]
		[InlineData(0, -1)]
		[InlineData(1, 0)]
		[InlineData(1, 1)]
		[InlineData(1, 2)]
		[InlineData(1, -1)]
		[InlineData(2, 0)]
		[InlineData(2, 1)]
		[InlineData(2, 2)]
		[InlineData(2, 3)]
		[InlineData(2, -1)]
		public async Task MarkEventsAsDeliveredCumulative_SingleThread_Async(int eventCount, int deliveredSequenceId)
		{
			Guard.InRangeAndNotNull(nameof(eventCount), eventCount, 0, 3);

			// Arrange
			var events = new[]
			{
				new TestWrappedEvent(1),
				new TestWrappedEvent(2),
				new TestWrappedEvent(3),
			}.Take(eventCount).ToArray();
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_1", events);

			// Act
			async Task ActionAsync()
			{
				await EventRepository.MarkEventsAsDeliveredCumulativeAsync(nameof(TestRoundAggregate), "MY_ID_1", deliveredSequenceId);
			}

			// Assert
			if (deliveredSequenceId < 0)
			{
				var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(ActionAsync);
				exception.ParamName.ShouldBe("deliveredSequenceId");
			}
			else if (eventCount < deliveredSequenceId)
			{
				var exception = await Assert.ThrowsAsync<ArgumentException>(ActionAsync);
				exception.ParamName.ShouldBe("deliveredSequenceId");
			}
			else
			{
				await ActionAsync();
				var undeliveredEvents = await EventRepository.ListUndeliveredEventsAsync();
				if (deliveredSequenceId < eventCount)
				{
					undeliveredEvents.Count.ShouldBe(1);
					undeliveredEvents[0].AggregateType.ShouldBe(nameof(TestRoundAggregate));
					undeliveredEvents[0].AggregateId.ShouldBe("MY_ID_1");
					undeliveredEvents[0].WrappedEvents.Cast<TestWrappedEvent>().ShouldBe(events.Skip(deliveredSequenceId));
				}
				else if (deliveredSequenceId == eventCount)
				{
					undeliveredEvents.ShouldBeEmpty();
				}
				else
				{
					throw new ApplicationException($"Unexpected code reached in '{nameof(MarkEventsAsDeliveredCumulative_SingleThread_Async)}'");
				}
			}
		}

		[Theory]
		[InlineData(0, 0, 0, 0)]
		[InlineData(0, 0, 1, 0)]
		[InlineData(0, 0, 0, 1)]
		[InlineData(0, 0, -1, 0)]
		[InlineData(0, 0, 0, -1)]
		[InlineData(1, 0, 0, 0)]
		[InlineData(1, 0, 0, 1)]
		[InlineData(1, 0, 1, 0)]
		[InlineData(1, 0, 1, 1)]
		[InlineData(1, 0, 1, -1)]
		[InlineData(1, 0, 2, 0)]
		[InlineData(1, 0, -1, 1)]
		[InlineData(0, 1, 0, 0)]
		[InlineData(0, 1, 0, 1)]
		[InlineData(0, 1, 1, 0)]
		[InlineData(0, 1, 1, 1)]
		[InlineData(0, 1, 1, -1)]
		[InlineData(0, 1, 0, 2)]
		[InlineData(0, 1, -1, 1)]
		[InlineData(1, 1, 0, 0)]
		[InlineData(1, 1, 0, 1)]
		[InlineData(1, 1, 1, 0)]
		[InlineData(1, 1, 1, 1)]
		[InlineData(1, 1, 1, -1)]
		[InlineData(1, 1, 0, 2)]
		[InlineData(1, 1, 1, 2)]
		[InlineData(1, 1, 2, 0)]
		[InlineData(1, 1, 2, 1)]
		[InlineData(1, 1, 2, 2)]
		[InlineData(1, 1, -1, 1)]
		[InlineData(1, 2, 0, 0)]
		[InlineData(1, 2, 0, 1)]
		[InlineData(1, 2, 1, 0)]
		[InlineData(1, 2, 1, 1)]
		[InlineData(1, 2, 1, -1)]
		[InlineData(1, 2, 0, 2)]
		[InlineData(1, 2, 1, 2)]
		[InlineData(1, 2, 1, 3)]
		[InlineData(1, 2, 2, 0)]
		[InlineData(1, 2, 2, 1)]
		[InlineData(1, 2, 2, 2)]
		[InlineData(1, 2, 2, 3)]
		[InlineData(1, 2, -1, 1)]
		[InlineData(2, 1, 0, 0)]
		[InlineData(2, 1, 0, 1)]
		[InlineData(2, 1, 1, 0)]
		[InlineData(2, 1, 1, 1)]
		[InlineData(2, 1, 1, -1)]
		[InlineData(2, 1, 0, 2)]
		[InlineData(2, 1, 1, 2)]
		[InlineData(2, 1, 1, 3)]
		[InlineData(2, 1, 2, 0)]
		[InlineData(2, 1, 2, 1)]
		[InlineData(2, 1, 2, 2)]
		[InlineData(2, 1, 3, 2)]
		[InlineData(2, 1, -1, 1)]
		[InlineData(2, 2, 0, 0)]
		[InlineData(2, 2, 0, 1)]
		[InlineData(2, 2, 1, 0)]
		[InlineData(2, 2, 1, 1)]
		[InlineData(2, 2, 1, -1)]
		[InlineData(2, 2, 0, 2)]
		[InlineData(2, 2, 0, 3)]
		[InlineData(2, 2, 1, 2)]
		[InlineData(2, 2, 1, 3)]
		[InlineData(2, 2, 2, 0)]
		[InlineData(2, 2, 2, 1)]
		[InlineData(2, 2, 2, 2)]
		[InlineData(2, 2, 2, 3)]
		[InlineData(2, 2, 3, 0)]
		[InlineData(2, 2, 3, 1)]
		[InlineData(2, 2, 3, 2)]
		[InlineData(2, 2, 3, 3)]
		[InlineData(2, 2, -1, 1)]
		public async Task MarkEventsAsDeliveredCumulative_SingleThreadTwoAggregates_Async(
			int aEventsCount,
			int bEventsCount,
			int aDeliveredSequenceIds,
			int bDeliveredSequenceIds)
		{
			Guard.InRangeAndNotNull(nameof(aEventsCount), aEventsCount, 0, 3);
			Guard.InRangeAndNotNull(nameof(bEventsCount), bEventsCount, 0, 3);

			// Arrange
			var aEvents = new[]
			{
				new TestWrappedEvent(1, "a1"),
				new TestWrappedEvent(2, "a2"),
				new TestWrappedEvent(3, "a3"),
			}.Take(aEventsCount).ToArray();
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_A", aEvents);
			var bEvents = new[]
			{
				new TestWrappedEvent(1, "b1"),
				new TestWrappedEvent(2, "b2"),
				new TestWrappedEvent(3, "b3"),
			}.Take(bEventsCount).ToArray();
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "MY_ID_B", bEvents);

			// Act
			async Task ActionA_Async()
			{
				await EventRepository.MarkEventsAsDeliveredCumulativeAsync(nameof(TestRoundAggregate), "MY_ID_A", aDeliveredSequenceIds);
			}
			async Task ActionB_Async()
			{
				await EventRepository.MarkEventsAsDeliveredCumulativeAsync(nameof(TestRoundAggregate), "MY_ID_B", bDeliveredSequenceIds);
			}

			// Assert
			async Task AssertAsync(TestWrappedEvent[] events, string id, int deliveredSequenceId, int eventCount, Func<Task> actionAsync)
			{
				if (deliveredSequenceId < 0)
				{
					var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(actionAsync);
					exception.ParamName.ShouldBe("deliveredSequenceId");
				}
				else if (eventCount < deliveredSequenceId)
				{
					var exception = await Assert.ThrowsAsync<ArgumentException>(actionAsync);
					exception.ParamName.ShouldBe("deliveredSequenceId");
				}
				else
				{
					await actionAsync();
					var undeliveredEvents = (await EventRepository.ListUndeliveredEventsAsync())
						.Where(a => a.AggregateId == id)
						.ToImmutableList();
					if (deliveredSequenceId < eventCount)
					{
						undeliveredEvents.Count.ShouldBe(1);
						undeliveredEvents[0].AggregateType.ShouldBe(nameof(TestRoundAggregate));
						undeliveredEvents[0].AggregateId.ShouldBe(id);
						undeliveredEvents[0].WrappedEvents.Cast<TestWrappedEvent>().ShouldBe(events.Skip(deliveredSequenceId));
					}
					else if (deliveredSequenceId == eventCount)
					{
						undeliveredEvents.ShouldBeEmpty();
					}
					else
					{
						throw new ApplicationException($"Unexpected code reached in '{nameof(MarkEventsAsDeliveredCumulative_SingleThreadTwoAggregates_Async)}'");
					}
				}
			}
			await AssertAsync(aEvents, "MY_ID_A", aDeliveredSequenceIds, aEventsCount, ActionA_Async);
			await AssertAsync(bEvents, "MY_ID_B", bDeliveredSequenceIds, bEventsCount, ActionB_Async);
		}

		[Theory]
		[InlineData(2, 1, 1, false, false)]
		[InlineData(3, 2, 1, true, false)]
		[InlineData(4, 2, 1, true, true)]
		[InlineData(3, 1, 1, false, true)]
		public async Task MarkUndeliveredSequenceIds_TryFixUndeliveredSequenceIdsAfterAppendConflict_RemoveUpdate_Async(
			int conflictedEvents,
			int appendedEvents,
			int confirmedSequenceId,
			bool updateInTryFix,
			bool conflictInTryFix)
		{
			// Arrange

			// Act
			async Task AppendAsync()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "ID_1", new TestWrappedEvent[]
				{
					new(1, "a1"),
					new(2, "a2"),
					new(3, "a3"),
					new(4, "a4"),
				}.Take(conflictedEvents));
			}
			// After first AppendEventsAsync() call marks events as undelivered but before
			// they are actually appended to the repository
			TestEventRepository.Append_MarkedUndeliveredCallback = async () =>
			{
				TestEventRepository.Append_MarkedUndeliveredCallback = null;
				// Competing append will succeed and trigger conflict of the first AppendEventsAsync() call
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "ID_1", new TestWrappedEvent[]
				{
					new(1, "b1"),
					new(2, "b2"),
				}.Take(appendedEvents));
				if (conflictInTryFix)
				{
					TestEventRepository.TryFixUndelivered_DetectedCallback = async () =>
					{
						TestEventRepository.TryFixUndelivered_DetectedCallback = null;
						await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "ID_1", new TestWrappedEvent[]
						{
							new(appendedEvents + 1, $"c{appendedEvents + 1}"),
						});
						appendedEvents++;
					};
				}
				await EventRepository.MarkEventsAsDeliveredCumulativeAsync(nameof(TestRoundAggregate), "ID_1", confirmedSequenceId);
			};

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(AppendAsync);
			(await TestEventRepository.DoMarkDelivered_UndeliveredConflictFixedSemaphore.WaitAsync(0))
				.ShouldBeTrue();
			if (conflictInTryFix)
			{
				(await TestEventRepository.TryFixUndelivered_UpdateConflictedSemaphore.WaitAsync(0))
					.ShouldBe(updateInTryFix);
				(await TestEventRepository.TryFixUndelivered_RemoveConflictedSemaphore.WaitAsync(0))
					.ShouldBe(!updateInTryFix);
			}
			else
			{
				(await TestEventRepository.TryFixUndelivered_UpdatedSemaphore.WaitAsync(0))
					.ShouldBe(updateInTryFix);
				(await TestEventRepository.TryFixUndelivered_RemovedSemaphore.WaitAsync(0))
					.ShouldBe(!updateInTryFix);
			}
			(await EventRepository.ListUndeliveredEventsAsync())
				.SelectMany(a => a.WrappedEvents)
				.Count()
				.ShouldBe(appendedEvents - confirmedSequenceId);
		}

		[Theory]
		[InlineData(4, 1)]
		public async Task MarkUndeliveredSequenceIds_TryFixUndeliveredSequenceIdsAfterAppendConflict_RemoveUpdate_2_Async(
			int conflictedEvents,
			int appendedEvents)
		{
			// Arrange

			// Act
			async Task AppendAsync()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "ID_1", new TestWrappedEvent[]
				{
					new(1, "a1"),
					new(2, "a2"),
					new(3, "a3"),
					new(4, "a4"),
				}.Take(conflictedEvents));
			}
			// After first AppendEventsAsync() call marks events as undelivered but before
			// they are actually appended to the repository
			TestEventRepository.Append_MarkedUndeliveredCallback = async () =>
			{
				TestEventRepository.Append_MarkedUndeliveredCallback = null;
				// Competing append will succeed and trigger conflict of the first AppendEventsAsync() call
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "ID_1", new TestWrappedEvent[]
				{
					new(1, "b1"),
				}.Take(appendedEvents));
			};
			// After second AppendEventsAsync() call appends events but before conflict of the first call
			TestEventRepository.Append_AppendedCallback = async () =>
			{
				TestEventRepository.Append_AppendedCallback = null;
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "ID_1", new TestWrappedEvent[]
				{
					new(2, "c2"),
				});
			};

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(AppendAsync);
		}

		public void Dispose()
		{
			TestEventRepository.Dispose();
		}
	}
}

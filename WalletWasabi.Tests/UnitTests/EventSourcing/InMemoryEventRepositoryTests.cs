using Shouldly;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Exceptions;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain;
using Xunit;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests.EventSourcing
{
	public class InMemoryEventRepositoryTests : IDisposable
	{
		private const string ID_1 = "ID_1";
		private const string ID_2 = "ID_2";
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
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events);

			// Assert
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1))
				.SequenceEqual(events));
			Assert.DoesNotContain(await EventRepository.ListUndeliveredEventsAsync(),
				a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == ID_1);
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
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events);

			// Assert
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1))
				.SequenceEqual(events));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == ID_1)
				.WrappedEvents
				.SequenceEqual(events));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { ID_1 }));
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
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events);

			// Assert
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1))
				.SequenceEqual(events));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == ID_1)
				.WrappedEvents
				.SequenceEqual(events));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { ID_1 }));
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
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events).ConfigureAwait(false);
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
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events);
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
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events);

			// Act
			async Task ActionAsync()
			{
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events);
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
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events1);
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events2);
			}

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(ActionAsync);
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1))
				.Cast<TestWrappedEvent>().SequenceEqual(events1));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == ID_1)
				.WrappedEvents.Cast<TestWrappedEvent>().SequenceEqual(events1));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { ID_1 }));
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
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events1);
			}
			async Task Append2Async()
			{
				Assert.True(await TestEventRepository.Append_Appended_Semaphore.WaitAsync(_semaphoreWaitTimeout));
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events2!);
			}
			async Task AppendInParallelAsync()
			{
				var task1 = Task.Run(Append1Async);
				var task2 = Task.Run(Append2Async);
				await Task.WhenAll(task1, task2);
			}
			async Task WaitForConflict()
			{
				Assert.True(await TestEventRepository.Append_Conflicted_Semaphore.WaitAsync(_semaphoreWaitTimeout));
			}
			TestEventRepository.Append_Appended_Callback = WaitForConflict;

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(AppendInParallelAsync);
			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1))
						.Cast<TestWrappedEvent>().SequenceEqual(events1));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == ID_1)
				.WrappedEvents.Cast<TestWrappedEvent>().SequenceEqual(events1));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { ID_1 }));
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
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events1);
			}
			async Task Append2Async()
			{
				Assert.True(await TestEventRepository.Append_Appended_Semaphore.WaitAsync(_semaphoreWaitTimeout));
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events2!);
			}
			async Task AppendInParallelAsync()
			{
				var task1 = Task.Run(Append1Async);
				var task2 = Task.Run(Append2Async);
				await Task.WhenAll(task1, task2);
			}
			async Task WaitForNoConflict()
			{
				Assert.False(await TestEventRepository.Append_Conflicted_Semaphore.WaitAsync(_semaphoreWaitTimeout));
			}
			TestEventRepository.Append_Appended_Callback = WaitForNoConflict;

			// no conflict
			await AppendInParallelAsync();

			Assert.True((await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1))
						.Cast<TestWrappedEvent>().SequenceEqual(events1.Concat(events2)));
			Assert.True((await EventRepository.ListUndeliveredEventsAsync())
				.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == ID_1)
				.WrappedEvents.Cast<TestWrappedEvent>().SequenceEqual(events1.Concat(events2)));
			Assert.True((await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
				.SequenceEqual(new[] { ID_1 }));
		}

		[Theory]
		[InlineData(nameof(TestInMemoryEventRepository.Append_Validated_Callback))]
		[InlineData(nameof(TestInMemoryEventRepository.Append_Appended_Callback))]
		public async Task ListEventsAsync_ConflictWithAppending_Async(string listOnCallback)
		{
			// Arrange
			var events1 = new[] { new TestWrappedEvent(1, "a"), new TestWrappedEvent(2, "a") };
			var events2 = new[] { new TestWrappedEvent(3, "b"), new TestWrappedEvent(4, "b") };
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events1);

			// Act
			IReadOnlyList<WrappedEvent> result = ImmutableList<WrappedEvent>.Empty;
			IReadOnlyList<WrappedEvent> result2 = ImmutableList<WrappedEvent>.Empty;
			async Task ListCallback()
			{
				result = await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1);
				result2 = (await EventRepository.ListUndeliveredEventsAsync())
					.First(a => a.AggregateType == nameof(TestRoundAggregate) && a.AggregateId == ID_1)
					.WrappedEvents;
			}
			switch (listOnCallback)
			{
				case nameof(TestInMemoryEventRepository.Append_Validated_Callback):
					TestEventRepository.Append_Validated_Callback = ListCallback;
					break;

				case nameof(TestInMemoryEventRepository.Append_Appended_Callback):
					TestEventRepository.Append_Appended_Callback = ListCallback;
					break;

				default:
					throw new ApplicationException($"unexpected value listOnCallback: '{listOnCallback}'");
			}
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events2);

			// Assert
			var expected = events1.AsEnumerable();
			switch (listOnCallback)
			{
				case nameof(TestInMemoryEventRepository.Append_Appended_Callback):
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
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events);

			// Act
			var result = await EventRepository.ListEventsAsync(
				nameof(TestRoundAggregate), ID_1, afterSequenceId, limit);

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
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events);
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_2, events);

			// Act
			var result = await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate));

			// Assert
			Assert.True(result.SequenceEqual(new[] { ID_1, ID_2 }));
		}

		[Theory]
		[InlineData("0", 0)]
		[InlineData("0", 1)]
		[InlineData("0", 2)]
		[InlineData("0", 3)]
		[InlineData(ID_1, 0)]
		[InlineData(ID_1, 1)]
		[InlineData(ID_1, 2)]
		[InlineData(ID_2, 0)]
		[InlineData(ID_2, 1)]
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
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events);
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_2, events);

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
		public async Task MarkEventsAsDeliveredCumulativeAsync_SingleThread_Async(int eventCount, int deliveredSequenceId)
		{
			Guard.InRangeAndNotNull(nameof(eventCount), eventCount, 0, 3);

			// Arrange
			var events = new[]
			{
				new TestWrappedEvent(1),
				new TestWrappedEvent(2),
				new TestWrappedEvent(3),
			}.Take(eventCount).ToArray();
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, events);

			// Act
			async Task ActionAsync()
			{
				await EventRepository.MarkEventsAsDeliveredCumulativeAsync(nameof(TestRoundAggregate), ID_1, deliveredSequenceId);
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
					undeliveredEvents[0].AggregateId.ShouldBe(ID_1);
					undeliveredEvents[0].WrappedEvents.Cast<TestWrappedEvent>().ShouldBe(events.Skip(deliveredSequenceId));
				}
				else if (deliveredSequenceId == eventCount)
				{
					undeliveredEvents.ShouldBeEmpty();
				}
				else
				{
					throw new ApplicationException($"Unexpected code reached in '{nameof(MarkEventsAsDeliveredCumulativeAsync_SingleThread_Async)}'");
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
		public async Task MarkEventsAsDeliveredCumulativeAsync_SingleThreadTwoAggregates_Async(
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
						throw new ApplicationException($"Unexpected code reached in '{nameof(MarkEventsAsDeliveredCumulativeAsync_SingleThreadTwoAggregates_Async)}'");
					}
				}
			}
			await AssertAsync(aEvents, "MY_ID_A", aDeliveredSequenceIds, aEventsCount, ActionA_Async);
			await AssertAsync(bEvents, "MY_ID_B", bDeliveredSequenceIds, bEventsCount, ActionB_Async);
		}

		private Func<Task> PrepareAppendWithConflict(
			int conflictedEvents,
			int appendedEvents,
			Func<Task>? beforeAppend = null,
			Func<Task>? afterAppend = null,
			int firstSequenceId = 1,
			string id = ID_1)
		{
			Guard.InRangeAndNotNull(nameof(conflictedEvents), conflictedEvents, 0, 5);
			Guard.InRangeAndNotNull(nameof(appendedEvents), appendedEvents, 0, 3);

			var appendAsync = async () =>
			{
				var aSId = firstSequenceId;
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), id, new TestWrappedEvent[]
				{
					new(aSId++, "a1"),
					new(aSId++, "a2"),
					new(aSId++, "a3"),
					new(aSId++, "a4"),
					new(aSId++, "a5"),
				}.Take(conflictedEvents));
			};

			// After first AppendEventsAsync() call marks events as undelivered but before
			// they are actually appended to the repository
			TestEventRepository.Append_MarkedUndelivered_Callback = async () =>
			{
				TestEventRepository.Append_MarkedUndelivered_Callback = null;

				if (beforeAppend is not null)
					await beforeAppend.Invoke();

				var bSId = firstSequenceId;

				// Competing append will succeed and trigger conflict of the first AppendEventsAsync() call
				await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), id, new TestWrappedEvent[]
				{
					new(bSId++, "b1"),
					new(bSId++, "b2"),
					new(bSId++, "b3"),
				}.Take(appendedEvents));

				if (afterAppend is not null)
					await afterAppend.Invoke();
			};

			return appendAsync;
		}

		private async Task Assert_MarkUndeliveredSemaphore_Async(
			int? started = null,
			int? got = null,
			int? undeliveredConflictKept = null,
			int? conflicted = null,
			int? ended = null)
		{
			if (started.HasValue)
			{
				await AssertSemaphoreAsync(
					TestEventRepository.MarkUndelivered_Started_Semaphore,
					started.Value,
					nameof(TestEventRepository.MarkUndelivered_Started_Semaphore));
			}
			if (got.HasValue)
			{
				await AssertSemaphoreAsync(
					TestEventRepository.MarkUndelivered_Got_Semaphore,
					got.Value,
					nameof(TestEventRepository.MarkUndelivered_Got_Semaphore));
			}
			if (undeliveredConflictKept.HasValue)
			{
				await AssertSemaphoreAsync(
					TestEventRepository.MarkUndelivered_UndeliveredConflictKept_Semaphore,
					undeliveredConflictKept.Value,
					nameof(TestEventRepository.MarkUndelivered_UndeliveredConflictKept_Semaphore));
			}
			if (conflicted.HasValue)
			{
				await AssertSemaphoreAsync(
					TestEventRepository.MarkUndelivered_Conflicted_Semaphore,
					conflicted.Value,
					nameof(TestEventRepository.MarkUndelivered_Conflicted_Semaphore));
			}
			if (ended.HasValue)
			{
				await AssertSemaphoreAsync(
					TestEventRepository.MarkUndelivered_Ended_Semaphore,
					ended.Value,
					nameof(TestEventRepository.MarkUndelivered_Ended_Semaphore));
			}
		}

		private async Task AssertSemaphoreAsync(SemaphoreSlim semaphore, int expected, string? name = null)
		{
			Guard.MinimumAndNotNull(nameof(expected), expected, 0);
			if (expected == 0)
			{
				(await semaphore.WaitAsync(0))
					.ShouldBeFalse($"semaphore '{name}' was expected to be '{expected}' but it wasn't");
			}
			else
			{
				for (var i = 0; i < expected; i++)
				{
					(await semaphore.WaitAsync(0))
						.ShouldBeTrue($"semaphore '{name}' was expected to be '{expected}' but it wasn't");
				}
				(await semaphore.WaitAsync(0))
					.ShouldBeFalse($"semaphore '{name}' was expected to be '{expected}' but it wasn't");
			}
		}

		[Fact]
		public async Task MarkUndeliveredSequenceIdsAsync_AppendConflict_ReturnOnPreviouEqDefault_Async()
		{
			// Arrange
			Func<Task> appendAsync = PrepareAppendWithConflict(1, 1);

			// Act
			async Task Act()
			{
				await appendAsync.Invoke();
			}

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(Act);
			await Assert_MarkUndeliveredSemaphore_Async(
				started: 2,
				undeliveredConflictKept: 0,
				ended: 0);
			var events = await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1);
			events.Count.ShouldBe(1);
			var undeliveredEvents = await EventRepository.ListUndeliveredEventsAsync();
			undeliveredEvents.Count.ShouldBe(1);
			undeliveredEvents[0].WrappedEvents.Count.ShouldBe(1);
		}

		[Fact]
		public async Task MarkUndeliveredSequenceIdsAsync_AppendConflict_UndeliveredConflictKept_Async()
		{
			// Arrange
			Func<Task> appendAsync = PrepareAppendWithConflict(2, 1);

			// Act
			async Task Act()
			{
				await appendAsync.Invoke();
			}

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(Act);
			await Assert_MarkUndeliveredSemaphore_Async(
				undeliveredConflictKept: 1,
				ended: 0);
			var events = await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1);
			events.Count.ShouldBe(1);
			var undeliveredEvents = await EventRepository.ListUndeliveredEventsAsync();
			undeliveredEvents.Count.ShouldBe(1);
			undeliveredEvents[0].WrappedEvents.Count.ShouldBe(1);
		}

		[Fact]
		public async Task MarkUndeliveredSequenceIdsAsync_AppendConflict_Updated_Async()
		{
			// Arrange
			Func<Task> appendAsync = PrepareAppendWithConflict(1, 2);

			// Act
			async Task Act()
			{
				await appendAsync.Invoke();
			}

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(Act);
			await Assert_MarkUndeliveredSemaphore_Async(
				conflicted: 0,
				ended: 1);
			var events = await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1);
			events.Count.ShouldBe(2);
			var undeliveredEvents = await EventRepository.ListUndeliveredEventsAsync();
			undeliveredEvents.Count.ShouldBe(1);
			undeliveredEvents[0].WrappedEvents.Count.ShouldBe(2);
		}

		[Fact]
		public async Task MarkUndeliveredSequenceIdsAsync_AppendConflict_Conflicted_Async()
		{
			// Arrange
			Func<Task> appendAsync = PrepareAppendWithConflict(1, 3,
				() =>
				{
					TestEventRepository.MarkUndelivered_Got_Callback = async () =>
					{
						TestEventRepository.MarkUndelivered_Got_Callback = null;

						TestEventRepository.Append_MarkedUndelivered_Callback = () =>
						{
							TestEventRepository.Append_MarkedUndelivered_Callback = null;

							throw new TestException();
						};

						await Assert.ThrowsAsync<TestException>(async () => await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), "ID_1", new TestWrappedEvent[]
						{
							new(1, "c1"),
							new(2, "c2"),
						}));
					};
					return Task.CompletedTask;
				});

			// Act
			async Task Act()
			{
				await appendAsync.Invoke();
			}

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(Act);
			await Assert_MarkUndeliveredSemaphore_Async(
				conflicted: 1,
				ended: 2);
			var events = await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1);
			events.Count.ShouldBe(3);
			var undeliveredEvents = await EventRepository.ListUndeliveredEventsAsync();
			undeliveredEvents.Count.ShouldBe(1);
			undeliveredEvents[0].WrappedEvents.Count.ShouldBe(3);
		}

		[Theory]
		[InlineData(0, 0)]
		[InlineData(1, 0)]
		[InlineData(1, 1)]
		[InlineData(2, 0)]
		[InlineData(2, 1)]
		[InlineData(2, 2)]
		[InlineData(3, 0)]
		[InlineData(3, 1)]
		[InlineData(3, 2)]
		[InlineData(3, 3)]
		public async Task ListUndeliveredEventsAsync_AppendConflict_NonEmptyResult_Async(
			int preEvents,
			int preConfirm)
		{
			Guard.InRangeAndNotNull(nameof(preEvents), preEvents, 0, 3);

			// Arrange
			await EventRepository.AppendEventsAsync(nameof(TestRoundAggregate), ID_1, new TestWrappedEvent[]
			{
				new(1,"_1"),
				new(2,"_2"),
				new(3,"_3"),
			}.Take(preEvents));
			IReadOnlyList<AggregateUndeliveredEvents>? undeliveredEvents = null;
			Func<Task>? afterAppend = null;
			Func<Task> appendAsync = PrepareAppendWithConflict(preEvents + 2, 1, firstSequenceId: preEvents + 1,
				beforeAppend: async () => await EventRepository.MarkEventsAsDeliveredCumulativeAsync(
					 nameof(TestRoundAggregate),
					 ID_1,
					 preConfirm),
				afterAppend: async () => await afterAppend!.Invoke());

			// Act
			async Task Act()
			{
				await appendAsync.Invoke();
			}
			afterAppend = async () =>
				undeliveredEvents = await EventRepository.ListUndeliveredEventsAsync();

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(Act);
			var events = await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1);
			events.Count.ShouldBe(preEvents + 1);
			undeliveredEvents.ShouldNotBeNull();
			undeliveredEvents.Count.ShouldBe(1);
			undeliveredEvents[0].WrappedEvents.Count.ShouldBe(preEvents - preConfirm + 1);
		}

		[Fact]
		public async Task ListUndeliveredEventsAsync_AppendConflict_EmptyResult_Async()
		{
			// Arrange
			Func<Task>? beforeAppend = null;
			Func<Task> appendAsync = PrepareAppendWithConflict(2, 1,
				beforeAppend: () =>
				{
					TestEventRepository.Append_MarkedUndelivered_Callback = async () =>
					{
						TestEventRepository.Append_MarkedUndelivered_Callback = null;

						await beforeAppend!.Invoke();
					};

					return Task.CompletedTask;
				});
			IReadOnlyList<AggregateUndeliveredEvents>? undeliveredEvents1 = null;
			IReadOnlyList<WrappedEvent>? events1 = null;

			// Act
			async Task ActAsync()
			{
				await appendAsync.Invoke();
			}
			beforeAppend = async () =>
			{
				events1 = await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1);
				undeliveredEvents1 = await EventRepository.ListUndeliveredEventsAsync();
			};

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(ActAsync);
			undeliveredEvents1.ShouldNotBeNull();
			undeliveredEvents1.Count.ShouldBe(0);
			var events2 = await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1);
			events2.Count.ShouldBe(1);
			var undeliveredEvents2 = await EventRepository.ListUndeliveredEventsAsync();
			undeliveredEvents2.Count.ShouldBe(1);
			undeliveredEvents2[0].WrappedEvents.Count.ShouldBe(1);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(2)]
		public async Task MarkEventsAsDeliveredCumulativeAsync_NonExistentAggregateId_Async(int deliveredSequenceId)
		{
			// Arrange

			// Act
			async Task ActAsync()
			{
				await EventRepository.MarkEventsAsDeliveredCumulativeAsync(nameof(TestRoundAggregate), ID_2, deliveredSequenceId);
			}

			// Assert
			if (0 < deliveredSequenceId)
				await Assert.ThrowsAsync<ArgumentException>(ActAsync);
			else
				await ActAsync();
		}

		[Theory]
		[InlineData(1, 1)]
		[InlineData(2, 1)]
		[InlineData(2, 2)]
		public async Task MarkEventsAsDeliveredCumulativeAsync_AppendConflict_UndeliveredNoConflict_Async(
			int appendedEvents,
			int deliveredSequenceId)
		{
			// Arrange
			Func<Task>? afterAppend = null;
			Func<Task> appendAsync = PrepareAppendWithConflict(appendedEvents + 1, appendedEvents,
				afterAppend: async () => await afterAppend!.Invoke());

			// Act
			async Task ActAsync()
			{
				await appendAsync.Invoke();
			}
			afterAppend = async () =>
				await EventRepository.MarkEventsAsDeliveredCumulativeAsync(nameof(TestRoundAggregate), ID_1, deliveredSequenceId);

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(ActAsync);
			await Assert_MarkDeliveredSemaphore_Async(
				started: 1,
				conflicted: 0,
				ended: 1);
			var events = await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1);
			events.Count.ShouldBe(appendedEvents);
			var undeliveredEvents = await EventRepository.ListUndeliveredEventsAsync();
			if (appendedEvents <= deliveredSequenceId)
			{
				undeliveredEvents.Count.ShouldBe(0);
			}
			else
			{
				undeliveredEvents.Count.ShouldBe(1);
				undeliveredEvents[0].WrappedEvents.Count.ShouldBe(appendedEvents - deliveredSequenceId);
			}
		}

		[Theory]
		[InlineData(1, 1, 1)]
		[InlineData(2, 1, 1)]
		[InlineData(2, 1, 2)]
		[InlineData(2, 2, 1)]
		[InlineData(2, 2, 2)]
		[InlineData(3, 1, 2)]
		[InlineData(3, 2, 1)]
		public async Task MarkEventsAsDeliveredCumulativeAsync_AppendConflict_UndeliveredConflict_Async(
			int appendedEvents,
			int deliveredSequenceId1,
			int deliveredSequenceId2)
		{
			// Arrange
			Func<Task>? afterAppend = null;
			Func<Task> appendAsync = PrepareAppendWithConflict(appendedEvents + 1, appendedEvents,
				afterAppend: async () => await afterAppend!.Invoke());

			// Act
			async Task ActAsync()
			{
				await appendAsync.Invoke();
			}
			afterAppend = async () =>
			{
				TestEventRepository.DoMarkDelivered_Got_Callback = async () =>
				{
					TestEventRepository.DoMarkDelivered_Got_Callback = null;

					await EventRepository.MarkEventsAsDeliveredCumulativeAsync(nameof(TestRoundAggregate), ID_1, deliveredSequenceId2);
				};
				await EventRepository.MarkEventsAsDeliveredCumulativeAsync(nameof(TestRoundAggregate), ID_1, deliveredSequenceId1);
			};

			// Assert
			await Assert.ThrowsAsync<OptimisticConcurrencyException>(ActAsync);
			await Assert_MarkDeliveredSemaphore_Async(
				conflicted: 1);
			var events = await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), ID_1);
			events.Count.ShouldBe(appendedEvents);
			var undeliveredEvents = await EventRepository.ListUndeliveredEventsAsync();
			if (appendedEvents <= Math.Max(deliveredSequenceId1, deliveredSequenceId2))
			{
				undeliveredEvents.Count.ShouldBe(0);
			}
			else
			{
				undeliveredEvents.Count.ShouldBe(1);
				undeliveredEvents[0].WrappedEvents.Count.ShouldBe(appendedEvents - Math.Max(deliveredSequenceId1, deliveredSequenceId2));
			}
		}

		private async Task Assert_MarkDeliveredSemaphore_Async(
			int? started = null,
			int? gotAggregateEvents = null,
			int? conflicted = null,
			int? ended = null)
		{
			if (started.HasValue)
			{
				await AssertSemaphoreAsync(
					TestEventRepository.MarkDelivered_Started_Semaphore,
					started.Value,
					nameof(TestEventRepository.MarkDelivered_Started_Semaphore));
			}
			if (gotAggregateEvents.HasValue)
			{
				await AssertSemaphoreAsync(
					TestEventRepository.MarkDelivered_GotAggregateEvents_Semaphore,
					gotAggregateEvents.Value,
					nameof(TestEventRepository.MarkDelivered_GotAggregateEvents_Semaphore));
			}
			if (conflicted.HasValue)
			{
				await AssertSemaphoreAsync(
					TestEventRepository.MarkDelivered_Conflicted_Semaphore,
					conflicted.Value,
					nameof(TestEventRepository.MarkDelivered_Conflicted_Semaphore));
			}
			if (ended.HasValue)
			{
				await AssertSemaphoreAsync(
					TestEventRepository.MarkDelivered_Ended_Semaphore,
					ended.Value,
					nameof(TestEventRepository.MarkDelivered_Ended_Semaphore));
			}
		}

		public void Dispose()
		{
			TestEventRepository.Dispose();
		}
	}
}

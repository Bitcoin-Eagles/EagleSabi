using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Exceptions;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.EventSourcing
{
	/// <summary>
	/// Thread safe without locks in memory event repository implementation
	/// </summary>
	public class InMemoryEventRepository : IEventRepository
	{
		private const int LIVE_LOCK_LIMIT = 10000;

		private static readonly IReadOnlyList<WrappedEvent> EmptyResult
			= ImmutableList<WrappedEvent>.Empty;

		private static readonly IReadOnlyList<string> EmptyIds
			= ImmutableList<string>.Empty;

		private static readonly IComparer<WrappedEvent> WrappedEventSequenceIdComparer
			= Comparer<WrappedEvent>.Create((a, b) => a.SequenceId.CompareTo(b.SequenceId));

		private ConcurrentDictionary<
			// aggregateType
			string,
			ConcurrentDictionary<
				// aggregateId
				string,
				AggregateEvents>> AggregatesEvents
		{ get; } = new();

		private ConcurrentDictionary<
			// aggregateType
			string,
			AggregateTypeIds> AggregatesIds
		{ get; } = new();

		private ConcurrentDictionary<
			AggregateKey,
			AggregateSequenceIds> UndeliveredSequenceIds
		{ get; } = new();

		/// <inheritdoc/>
		public async Task AppendEventsAsync(
			string aggregateType,
			string aggregateId,
			IEnumerable<WrappedEvent> wrappedEvents)
		{
			Guard.NotNullOrEmpty(nameof(aggregateType), aggregateType);
			Guard.NotNullOrEmpty(nameof(aggregateId), aggregateId);
			Guard.NotNull(nameof(wrappedEvents), wrappedEvents);

			var wrappedEventsList = wrappedEvents.ToList().AsReadOnly();
			if (wrappedEventsList.Count == 0)
				return;

			var firstSequenceId = wrappedEventsList[0].SequenceId;
			var lastSequenceId = wrappedEventsList[^1].SequenceId;

			if (firstSequenceId <= 0)
				throw new ArgumentException("First event sequenceId is not natural number.", nameof(wrappedEvents));
			if (lastSequenceId <= 0)
				throw new ArgumentException("Last event sequenceId is not a positive integer.", nameof(wrappedEvents));
			if (lastSequenceId - firstSequenceId + 1 != wrappedEventsList.Count)
				throw new ArgumentException("Event sequence ids are inconsistent.", nameof(wrappedEvents));

			var aggregatesEvents = AggregatesEvents.GetOrAdd(aggregateType, _ => new());
			var prevEvents = aggregatesEvents.GetOrAdd(
				aggregateId,
				_ => new(0, ImmutableList<WrappedEvent>.Empty));

			if (prevEvents.TailSequenceId + 1 < firstSequenceId)
				throw new ArgumentException($"Invalid firstSequenceId (gap in sequence ids) expected: '{prevEvents.TailSequenceId + 1}' given: '{firstSequenceId}'.", nameof(wrappedEvents));

			await Append_Validated().ConfigureAwait(false); // no action

			var newEvents = prevEvents.Events.AddRange(wrappedEventsList);
			var newValue = new AggregateEvents(lastSequenceId, newEvents);
			var comparisonValue = prevEvents with { TailSequenceId = firstSequenceId - 1 };
			var conflict = prevEvents.TailSequenceId != comparisonValue.TailSequenceId;

			if (!conflict)
			{
				await MarkUndeliveredSequenceIdsAsync(aggregateType, aggregateId, firstSequenceId, lastSequenceId, prevEvents)
					.ConfigureAwait(false);

				await Append_MarkedUndelivered().ConfigureAwait(false); // no action
			}

			// Atomically detect conflict and replace TailSequenceId and append events.
			// NOTE: for robustness and unification we call it even if we already know about conflict
			// because there can also be a conflict that we don't know about and can't know about yet.
			if (!aggregatesEvents.TryUpdate(aggregateId, newValue, comparisonValue))
			{
				await Append_Conflicted().ConfigureAwait(false); // no action
				throw new OptimisticConcurrencyException(
					$"Conflict while committing events. Retry command. aggregate: '{aggregateType}' id: '{aggregateId}'");
			}
			if (conflict)
				throw new AssertionFailedException($"Unexpected code reached in '{nameof(AppendEventsAsync)}'. (conflict: '{conflict}')");

			await Append_Appended().ConfigureAwait(false); // no action

			// If it is a first event for given aggregate.
			if (prevEvents.TailSequenceId == 0)
				// Add index of aggregate id into the dictionary.
				IndexNewAggregateId(aggregateType, aggregateId);
		}

		/// <inheritdoc/>
		public Task<IReadOnlyList<WrappedEvent>> ListEventsAsync(
			string aggregateType,
			string aggregateId,
			long afterSequenceId = 0,
			int? maxCount = null)
		{
			Guard.NotNull(nameof(aggregateType), aggregateType);
			Guard.NotNull(nameof(aggregateId), aggregateId);
			if (AggregatesEvents.TryGetValue(aggregateType, out var aggregateEventsBatches) &&
				aggregateEventsBatches.TryGetValue(aggregateId, out var value))
			{
				var result = value.Events;

				if (afterSequenceId > 0)
				{
					var dummyEvent = new WrappedEvent<IEvent>("", "", afterSequenceId, null!, Guid.Empty);
					var foundIndex = result.BinarySearch(dummyEvent, WrappedEventSequenceIdComparer);
					if (foundIndex < 0)
						// Note: this is because of BinarySearch() documented implementation
						// returns "bitwise complement"
						// see: https://docs.microsoft.com/en-us/dotnet/api/system.collections.immutable.immutablelist-1.binarysearch
						// The zero-based index of item in the sorted List, if item is found;
						// otherwise, a negative number that is the bitwise complement
						// of the index of the next element that is larger than item or,
						// if there is no larger element, the bitwise complement of Count.
						foundIndex = ~foundIndex;
					else
						foundIndex++;
					result = result.GetRange(foundIndex, result.Count - foundIndex);
				}
				if (maxCount < result.Count)
					result = result.GetRange(0, maxCount.Value);
				return Task.FromResult((IReadOnlyList<WrappedEvent>)result);
			}
			return Task.FromResult(EmptyResult);
		}

		/// <inheritdoc/>
		public async Task MarkEventsAsDeliveredCumulativeAsync(string aggregateType, string aggregateId, long deliveredSequenceId)
		{
			Guard.MinimumAndNotNull(nameof(deliveredSequenceId), deliveredSequenceId, 0);
			if (deliveredSequenceId == 0)
				return;

			var aggregateKey = new AggregateKey(aggregateType, aggregateId);
			var liveLockLimit = LIVE_LOCK_LIMIT;
			var conflict = false;
			do
			{
				if (liveLockLimit-- <= 0)
					throw new AssertionFailedException("Live lock detected.");
				if (conflict)
					await MarkDelivered_Conflicted().ConfigureAwait(false); // no action
				else
					await MarkDelivered_Started().ConfigureAwait(false); // no action

				// If deliveredSequenceId is too high
				if (!AggregatesEvents.TryGetValue(aggregateType, out var aggregates)
						|| !aggregates.TryGetValue(aggregateId, out var aggregateEvents)
						|| aggregateEvents.TailSequenceId < deliveredSequenceId)
				{
					throw new ArgumentException(
						$"{nameof(deliveredSequenceId)} is greater than last appended event's SequenceId (than '{nameof(AggregateEvents.TailSequenceId)}')",
						nameof(deliveredSequenceId));
				}

				await MarkDelivered_GotAggregateEvents().ConfigureAwait(false); // no action

				conflict = !await TryDoMarkEventsAsDeliveredComulativeAsync(aggregateKey, aggregateEvents, deliveredSequenceId)
					.ConfigureAwait(false);
			}
			while (conflict);

			await MarkDelivered_Ended().ConfigureAwait(false); // no action
		}

		/// <inheritdoc/>
		public async Task<IReadOnlyList<AggregateUndeliveredEvents>> ListUndeliveredEventsAsync(int? maxCount = null)
		{
			if (maxCount < 1)
				throw new ArgumentOutOfRangeException(nameof(maxCount), $"'{maxCount}' is not positive integer.");
			var result = new List<AggregateUndeliveredEvents>();

			foreach (var (key, sequenceIds) in UndeliveredSequenceIds)
			{
				var events = await ListEventsAsync(key.AggregateType, key.AggregateId, sequenceIds.DeliveredSequenceId, maxCount)
					.ConfigureAwait(false);

				if (0 < events.Count)
					result.Add(new AggregateUndeliveredEvents(key.AggregateType, key.AggregateId, events));

				maxCount -= events.Count;
				if (maxCount <= 0)
					break;
			}
			return result.AsReadOnly();
		}

		/// <inheritdoc/>
		public Task<IReadOnlyList<string>> ListAggregateIdsAsync(
			string aggregateType,
			string? afterAggregateId = null,
			int? maxCount = null)
		{
			if (AggregatesIds.TryGetValue(aggregateType, out var aggregateIds))
			{
				var ids = aggregateIds.Ids;
				var foundIndex = 0;
				if (afterAggregateId != null)
				{
					foundIndex = ids.IndexOf(afterAggregateId);
					if (foundIndex < 0)
						foundIndex = ~foundIndex;
					else
						foundIndex++;
				}
				List<string> result = new();
				var afterLastIndex = maxCount.HasValue
					? Math.Min(foundIndex + maxCount.Value, ids.Count)
					: ids.Count;
				for (var i = foundIndex; i < afterLastIndex; i++)
					result.Add(ids[i]);
				return Task.FromResult((IReadOnlyList<string>)result.AsReadOnly());
			}
			return Task.FromResult(EmptyIds);
		}

		private void IndexNewAggregateId(string aggregateType, string aggregateId)
		{
			var tailIndex = 0L;
			ImmutableSortedSet<string> aggregateIds;
			ImmutableSortedSet<string> newAggregateIds;
			var liveLockLimit = LIVE_LOCK_LIMIT;
			do
			{
				if (liveLockLimit-- <= 0)
					throw new AssertionFailedException("Live lock detected.");
				(tailIndex, aggregateIds) = AggregatesIds.GetOrAdd(aggregateType, _ => new(0, ImmutableSortedSet<string>.Empty));
				newAggregateIds = aggregateIds.Add(aggregateId);
				if (newAggregateIds.Count == aggregateIds.Count)
					throw new AssertionFailedException($"Aggregate id duplicate detected in '{nameof(InMemoryEventRepository)}.{nameof(IndexNewAggregateId)}'");
			}
			while (!AggregatesIds.TryUpdate(
				key: aggregateType,
				newValue: new AggregateTypeIds(tailIndex + 1, newAggregateIds),
				comparisonValue: new AggregateTypeIds(tailIndex, aggregateIds)));
		}

		private async Task MarkUndeliveredSequenceIdsAsync(
			string aggregateType,
			string aggregateId,
			long transactionFirstSequenceId,
			long transactionLastSequenceId,
			AggregateEvents aggregateEvents)
		{
			if (transactionLastSequenceId < transactionFirstSequenceId)
				throw new ArgumentException($"'{nameof(transactionLastSequenceId)}' < '{nameof(transactionFirstSequenceId)}'");
			if (transactionFirstSequenceId <= aggregateEvents.TailSequenceId)
			{
				throw new ArgumentException($"There is an optimistic concurrency conflict. So this call is pointless.",
					nameof(transactionFirstSequenceId));
			}

			var aggregateKey = new AggregateKey(aggregateType, aggregateId);
			var liveLockLimit = LIVE_LOCK_LIMIT;
			AggregateSequenceIds? previous;
			var conflict = false;
			do
			{
				if (liveLockLimit-- <= 0)
					throw new AssertionFailedException("Live lock detected.");
				if (conflict)
					await MarkUndelivered_Conflicted().ConfigureAwait(false); // no action
				else
					await MarkUndelivered_Started().ConfigureAwait(false); // no action

				var defaultValue = new AggregateSequenceIds(
					transactionFirstSequenceId - 1,
					transactionFirstSequenceId,
					transactionLastSequenceId);

				previous = UndeliveredSequenceIds.GetOrAdd(aggregateKey, defaultValue);

				await MarkUndelivered_Got().ConfigureAwait(false); // no action

				// If key was newly added to the dictionary we are done.
				if (previous == defaultValue)
				{
					return;
				}
				// If there is already greater TransactionLastSequenceId
				// and conflict has not yet been detected we need to keep the greater one
				else if (transactionLastSequenceId < previous.TransactionLastSequenceId
					&& !IsUndeliveredSequenceIdsConflicted(aggregateEvents, previous))
				{
					await MarkUndelivered_UndeliveredConflictKept().ConfigureAwait(false); // no action

					return;
				}
				else if (transactionFirstSequenceId < previous.TransactionFirstSequenceId)
				{
					throw new ArgumentException($"'{nameof(transactionFirstSequenceId)}' < previous.TransactionFirstSequenceId",
						nameof(transactionFirstSequenceId));
				}

				conflict = !UndeliveredSequenceIds.TryUpdate(
					key: aggregateKey,
					newValue: new(previous.DeliveredSequenceId, transactionFirstSequenceId, transactionLastSequenceId),
					comparisonValue: previous);
			}
			while (conflict);

			await MarkUndelivered_Ended().ConfigureAwait(false); // no action
		}

		private async Task<bool> TryDoMarkEventsAsDeliveredComulativeAsync(
			AggregateKey aggregateKey,
			AggregateEvents aggregateEvents,
			long deliveredSequenceId)
		{
			await DoMarkDelivered_Entered().ConfigureAwait(false); // no action

			if (UndeliveredSequenceIds.TryGetValue(aggregateKey, out var previous))
			{
				await DoMarkDelivered_Got().ConfigureAwait(false); // no action

				if (previous.TransactionLastSequenceId < aggregateEvents.TailSequenceId)
					throw new AssertionFailedException($"'{nameof(UndeliveredSequenceIds)}' is inconsistnet with '{nameof(AggregatesEvents)}'. '{nameof(AggregateSequenceIds.TransactionLastSequenceId)}' is smaller than '{nameof(AggregateEvents.TailSequenceId)}'. (aggregateKey: '{aggregateKey}')");

				var transactionLastSequenceId = previous.TransactionLastSequenceId;

				if (IsUndeliveredSequenceIdsConflicted(aggregateEvents, previous))
				{
					transactionLastSequenceId = aggregateEvents.TailSequenceId;

					await DoMarkDelivered_UndeliveredConflictFixed().ConfigureAwait(false); // no action
				}

				// If there is already greater deliveredSequenceId keep it.
				if (deliveredSequenceId < previous.DeliveredSequenceId)
					deliveredSequenceId = previous.DeliveredSequenceId;

				var newValue = previous with
				{
					DeliveredSequenceId = deliveredSequenceId,
					TransactionLastSequenceId = transactionLastSequenceId
				};

				// If nothing has changed we are done.
				if (newValue == previous)
					return true;

				// If all events have been delivered
				if (transactionLastSequenceId == deliveredSequenceId)
				{
					return UndeliveredSequenceIds.TryRemove(KeyValuePair.Create(aggregateKey, previous));
				}
				// If some events remain to be delivered
				else if (deliveredSequenceId < transactionLastSequenceId)
				{
					return UndeliveredSequenceIds.TryUpdate(aggregateKey, newValue, previous);
				}
				else
				{
					// At this point one of the previous exceptions should have been thrown.
					throw new AssertionFailedException($"Unexpected code reached in '{nameof(TryDoMarkEventsAsDeliveredComulativeAsync)}'.");
				}
			}
			else
			{
				// 'deliveredSequenceId' has been already marked so we are done.
				return true;
			}
		}

		private bool IsUndeliveredSequenceIdsConflicted(AggregateEvents aggregateEvents, AggregateSequenceIds sequenceIds)
		{
			// If there has been conflict previously in AppendEventsAsync()
			return sequenceIds.TransactionFirstSequenceId <= aggregateEvents.TailSequenceId
				// and TransactionLastSequenceId has been speculativelly set too high
				&& aggregateEvents.TailSequenceId < sequenceIds.TransactionLastSequenceId;
		}

		#region Hooks

		// Hook for parallel critical section testing.
		protected virtual Task Append_Validated()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task Append_MarkedUndelivered()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task Append_Conflicted()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task Append_Appended()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task MarkDelivered_Started()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task MarkDelivered_GotAggregateEvents()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task MarkDelivered_Conflicted()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task MarkDelivered_Ended()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task MarkUndelivered_Started()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task MarkUndelivered_Got()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task MarkUndelivered_UndeliveredConflictKept()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task MarkUndelivered_Conflicted()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task MarkUndelivered_Ended()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task DoMarkDelivered_Entered()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task DoMarkDelivered_Got()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task DoMarkDelivered_UndeliveredConflictFixed()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		#endregion Hooks
	}
}

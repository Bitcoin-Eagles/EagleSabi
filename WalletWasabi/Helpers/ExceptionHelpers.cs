using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Helpers
{
	public static class ExceptionHelpers
	{
		/// <summary>
		/// Executes all actions regardless if one throws an exception.
		/// Then throws AggregateException containing all exceptions if any.
		/// Unpacks one level of AggregateException thrown from an action.
		/// </summary>
		public static void AggregateExceptions(params Action[] actions)
		{
			AggregateExceptions(actions.AsEnumerable());
		}

		/// <summary>
		/// Executes all actions regardless if one throws an exception.
		/// Then throws AggregateException containing all exceptions if any.
		/// Unpacks one level of AggregateException thrown from an action.
		/// </summary>
		public static void AggregateExceptions(string message, params Action[] actions)
		{
			AggregateExceptions(actions.AsEnumerable(), message);
		}

		/// <summary>
		/// Executes all actions regardless if one throws an exception.
		/// Then throws AggregateException containing all exceptions if any.
		/// Unpacks one level of AggregateException thrown from an action.
		/// </summary>
		public static void AggregateExceptions(IEnumerable<Action> actions, string? message = null)
		{
			var exceptions = new List<Exception>();
			foreach (var action in actions)
			{
				try { action.Invoke(); }
				catch (AggregateException excp)
				{
					exceptions.AddRange(excp.InnerExceptions);
				}
				catch (Exception excp)
				{
					exceptions.Add(excp);
				}
			}
			if (exceptions.Count == 1 && message is null)
				ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
			else if (1 < exceptions.Count)
				throw new AggregateException(message, exceptions);
		}

		/// <summary>
		/// Executes all actions regardless if one throws an exception.
		/// Then throws AggregateException containing all exceptions if any.
		/// Unpacks one level of AggregateException thrown from an action.
		/// </summary>
		public static async Task AggregateExceptionsAsync(string message, params Func<Task>[] tasks)
		{
			await AggregateExceptionsAsync(tasks.AsEnumerable(), message).ConfigureAwait(false);
		}

		/// <summary>
		/// Executes all actions regardless if one throws an exception.
		/// Then throws AggregateException containing all exceptions if any.
		/// Unpacks one level of AggregateException thrown from an action.
		/// </summary>
		public static async Task AggregateExceptionsAsync(params Func<Task>[] tasks)
		{
			await AggregateExceptionsAsync(tasks.AsEnumerable()).ConfigureAwait(false);
		}

		/// <summary>
		/// Executes all actions regardless if one throws an exception.
		/// Then throws AggregateException containing all exceptions if any.
		/// Unpacks one level of AggregateException thrown from an action.
		/// </summary>
		/// <exception cref="OperationCanceledException">if <paramref name="cancellationToken"/> requested cancellation</exception>
		/// <exception cref="ObjectDisposedException">if <paramref name="cancellationToken"/> is disposed</exception>
		public static async Task AggregateExceptionsAsync(IEnumerable<Func<Task>> tasks, string? message = null, CancellationToken cancellationToken = default)
		{
			var exceptions = new List<Exception>();
			foreach (var task in tasks)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try { await task.Invoke().ConfigureAwait(false); }
				catch (AggregateException excp)
				{
					exceptions.AddRange(excp.InnerExceptions);
				}
				catch (Exception excp)
				{
					exceptions.Add(excp);
				}
			}
			if (exceptions.Count == 1 && message is null)
				ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
			else if (1 < exceptions.Count)
				throw new AggregateException(message, exceptions);
		}
	}
}

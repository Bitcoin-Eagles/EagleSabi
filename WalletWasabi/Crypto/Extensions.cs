using NBitcoin;
using NBitcoin.Secp256k1;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace System.Linq
{
	public static class Extensions
	{
		public static Scalar Sum(this IEnumerable<Scalar> scalars) =>
			scalars.Aggregate(Scalar.Zero, (s, acc) => s + acc);

		public static GroupElement Sum(this IEnumerable<GroupElement> groupElements) =>
			groupElements.Aggregate(GroupElement.Infinity, (ge, acc) => ge + acc);

		public static Money ToMoney(this Scalar scalar) =>
			Money.Satoshis(scalar.ToLong());

		public static ulong ToUlong(this Scalar scalar) =>
			((ulong)scalar.d1 << 32) | scalar.d0;

		public static long ToLong(this Scalar scalar) =>
			checked((long)scalar.ToUlong());

		public static IEnumerable<TResult> Zip<TFirst, TSecond, TThird, TResult>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second, IEnumerable<TThird> third, Func<TFirst, TSecond, TThird, TResult> resultSelector)
		{
			Guard.NotNull(nameof(first), first);
			Guard.NotNull(nameof(second), second);
			Guard.NotNull(nameof(third), third);
			Guard.NotNull(nameof(resultSelector), resultSelector);
			using var e1 = first.GetEnumerator();
			using var e2 = second.GetEnumerator();
			using var e3 = third.GetEnumerator();
			while (e1.MoveNext() && e2.MoveNext() && e3.MoveNext())
			{
				yield return resultSelector(e1.Current, e2.Current, e3.Current);
			}
		}

		/// <summary>
		/// Executes <paramref name="action"/> on each element of the <paramref name="list"/>
		/// regardless if one throws an exception. Then throws AggregateException
		/// containing all exceptions if any. Unpacks one level of AggregateException
		/// thrown from an action.
		/// </summary>
		public static void ForEachAggregatingExceptions<T>(this IEnumerable<T> list, Action<T> action, string? message = null)
		{
			ExceptionHelpers.AggregateExceptions(list.Select(a => (Action)(() => action(a))), message);
		}

		/// <summary>
		/// Executes <paramref name="action"/> on each element of the <paramref name="list"/>
		/// regardless if one throws an exception. Then throws AggregateException
		/// containing all exceptions if any. Unpacks one level of AggregateException
		/// thrown from an action.
		/// </summary>
		public static async Task ForEachAggregatingExceptionsAsync<T>(this IEnumerable<T> list, Func<T, Task> action, string? message = null)
		{
			await ExceptionHelpers.AggregateExceptionsAsync(
				list.Select(a => (Func<Task>)(async () => await action.Invoke(a).ConfigureAwait(false))),
				message)
				.ConfigureAwait(false);
		}
	}
}

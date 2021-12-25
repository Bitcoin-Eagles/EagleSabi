using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class AliceClient
	{
		private AliceClient(
			Guid aliceId,
			RoundState roundState,
			ArenaClient arenaClient,
			ISpendableSmartCoin coin,
			IEnumerable<Credential> issuedAmountCredentials,
			IEnumerable<Credential> issuedVsizeCredentials)
		{
			AliceId = aliceId;
			RoundId = roundState.Id;
			ArenaClient = arenaClient;
			SpendableCoin = coin;
			FeeRate = roundState.FeeRate;
			IssuedAmountCredentials = issuedAmountCredentials;
			IssuedVsizeCredentials = issuedVsizeCredentials;
			MaxVsizeAllocationPerAlice = roundState.MaxVsizeAllocationPerAlice;
			ConfirmationTimeout = roundState.ConnectionConfirmationTimeout / 2;
		}

		public Guid AliceId { get; }
		public uint256 RoundId { get; }
		private ArenaClient ArenaClient { get; }
		public ISpendableSmartCoin SpendableCoin { get; }
		private FeeRate FeeRate { get; }
		public IEnumerable<Credential> IssuedAmountCredentials { get; private set; }
		public IEnumerable<Credential> IssuedVsizeCredentials { get; private set; }
		private long MaxVsizeAllocationPerAlice { get; }
		private TimeSpan ConfirmationTimeout { get; }

		[Obsolete("overload for garbage test compatibility")]
		public static Task<AliceClient> CreateRegisterAndConfirmInputAsync(
			RoundState roundState,
			ArenaClient arenaClient,
			SmartCoin coin,
			BitcoinSecret bitcoinSecret,
			Key identificationKey,
			RoundStateUpdater roundStatusUpdater,
			CancellationToken cancellationToken)
			=> CreateRegisterAndConfirmInputAsync(
				roundState,
				arenaClient,
				new SpendableSmartCoin(coin, bitcoinSecret, identificationKey),
				roundStatusUpdater,
				cancellationToken);

		public static async Task<AliceClient> CreateRegisterAndConfirmInputAsync(
			RoundState roundState,
			ArenaClient arenaClient,
			ISpendableSmartCoin coin,
			RoundStateUpdater roundStatusUpdater,
			CancellationToken cancellationToken)
		{
			AliceClient? aliceClient = null;
			try
			{
				aliceClient = await RegisterInputAsync(roundState, arenaClient, coin, cancellationToken).ConfigureAwait(false);
				await aliceClient.ConfirmConnectionAsync(roundStatusUpdater, cancellationToken).ConfigureAwait(false);

				Logger.LogInfo($"Round ({aliceClient.RoundId}), Alice ({aliceClient.AliceId}): Connection successfully confirmed.");
			}
			catch (OperationCanceledException)
			{
				if (aliceClient is { })
				{
					await aliceClient.TryToUnregisterAlicesAsync(CancellationToken.None).ConfigureAwait(false);
				}

				throw;
			}

			return aliceClient;
		}

		private static async Task<AliceClient> RegisterInputAsync(RoundState roundState, ArenaClient arenaClient, ISpendableSmartCoin coin, CancellationToken cancellationToken)
		{
			AliceClient? aliceClient;
			try
			{
				var ownershipProof = coin.GenerateOwnershipProof(new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", roundState.Id));
				var response = await arenaClient.RegisterInputAsync(roundState.Id, coin.Outpoint, ownershipProof, cancellationToken).ConfigureAwait(false);
				aliceClient = new(response.Value, roundState, arenaClient, coin, response.IssuedAmountCredentials, response.IssuedVsizeCredentials);
				coin.CoinJoinInProgress = true;

				Logger.LogInfo($"Round ({roundState.Id}), Alice ({aliceClient.AliceId}): Registered {coin.Outpoint}.");
			}
			catch (System.Net.Http.HttpRequestException ex)
			{
				if (ex.InnerException is WabiSabiProtocolException wpe)
				{
					switch (wpe.ErrorCode)
					{
						case WabiSabiProtocolErrorCode.InputSpent:
							coin.SpentAccordingToBackend = true;
							Logger.LogInfo($"{coin.Outpoint} is spent according to the backend. The wallet is not fully synchronized or corrupted.");
							break;

						case WabiSabiProtocolErrorCode.InputBanned:
							coin.BannedUntilUtc = DateTimeOffset.UtcNow.AddDays(1);
							coin.SetIsBanned();
							Logger.LogInfo($"{coin.Outpoint} is banned.");
							break;

						case WabiSabiProtocolErrorCode.InputNotWhitelisted:
							coin.SpentAccordingToBackend = false;
							Logger.LogWarning($"{coin.Outpoint} cannot be registered in the blame round.");
							break;

						case WabiSabiProtocolErrorCode.AliceAlreadyRegistered:
							Logger.LogInfo($"{coin.Outpoint} was already registered.");
							break;
					}
				}
				throw;
			}

			return aliceClient;
		}

		private async Task ConfirmConnectionAsync(RoundStateUpdater roundStatusUpdater, CancellationToken cancellationToken)
		{
			long[] amountsToRequest = { SpendableCoin.EffectiveValue(FeeRate).Satoshi };
			long[] vsizesToRequest = { MaxVsizeAllocationPerAlice - SpendableCoin.ScriptPubKey.EstimateInputVsize() };

			do
			{
				using CancellationTokenSource timeout = new(ConfirmationTimeout);
				using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

				try
				{
					await roundStatusUpdater
						.CreateRoundAwaiter(
							RoundId,
							roundState => roundState.Phase == Phase.ConnectionConfirmation,
							cts.Token)
						.ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					cancellationToken.ThrowIfCancellationRequested();
				}
			}
			while (!await TryConfirmConnectionAsync(amountsToRequest, vsizesToRequest, cancellationToken).ConfigureAwait(false));
		}

		private async Task<bool> TryConfirmConnectionAsync(IEnumerable<long> amountsToRequest, IEnumerable<long> vsizesToRequest, CancellationToken cancellationToken)
		{
			var totalFeeToPay = FeeRate.GetFee(SpendableCoin.ScriptPubKey.EstimateInputVsize());
			var totalAmount = SpendableCoin.Amount;
			var effectiveAmount = totalAmount - totalFeeToPay;

			if (effectiveAmount <= Money.Zero)
			{
				throw new InvalidOperationException($"Round({ RoundId }), Alice({ AliceId}): Adding this input is uneconomical.");
			}

			var response = await ArenaClient
				.ConfirmConnectionAsync(
					RoundId,
					AliceId,
					amountsToRequest,
					vsizesToRequest,
					IssuedAmountCredentials,
					IssuedVsizeCredentials,
					cancellationToken)
				.ConfigureAwait(false);

			IssuedAmountCredentials = response.IssuedAmountCredentials;
			IssuedVsizeCredentials = response.IssuedVsizeCredentials;

			var isConfirmed = response.Value;

			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Connection confirmed.");
			return isConfirmed;
		}

		public async Task TryToUnregisterAlicesAsync(CancellationToken cancellationToken)
		{
			try
			{
				await RemoveInputAsync(cancellationToken).ConfigureAwait(false);
				SpendableCoin.CoinJoinInProgress = false;
				Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Unregistered {SpendableCoin.Outpoint}.");
			}
			catch (System.Net.Http.HttpRequestException ex)
			{
				if (ex.InnerException is WabiSabiProtocolException wpe)
				{
					switch (wpe.ErrorCode)
					{
						case WabiSabiProtocolErrorCode.RoundNotFound:
							SpendableCoin.CoinJoinInProgress = false;
							Logger.LogInfo($"{SpendableCoin.Outpoint} the round was not found. Nothing to unregister.");
							break;

						case WabiSabiProtocolErrorCode.WrongPhase:
							Logger.LogInfo($"{SpendableCoin.Outpoint} could not be unregistered at this phase (too late).");
							break;
					}
				}

				// Log and swallow the exception because there is nothing else that can be done here.
				Logger.LogWarning($"{SpendableCoin.Outpoint} unregistration failed with {ex}.");
			}
		}

		public void Finish()
		{
			SpendableCoin.CoinJoinInProgress = false;
		}

		public async Task RemoveInputAsync(CancellationToken cancellationToken)
		{
			await ArenaClient.RemoveInputAsync(RoundId, AliceId, cancellationToken).ConfigureAwait(false);
			SpendableCoin.CoinJoinInProgress = false;
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Inputs removed.");
		}

		public async Task SignTransactionAsync(Transaction unsignedCoinJoin, CancellationToken cancellationToken)
		{
			await ArenaClient.SignTransactionAsync(RoundId, SpendableCoin, unsignedCoinJoin, cancellationToken).ConfigureAwait(false);

			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Posted a signature.");
		}

		public async Task ReadyToSignAsync(CancellationToken cancellationToken)
		{
			await ArenaClient.ReadyToSignAsync(RoundId, AliceId, cancellationToken).ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Ready to sign.");
		}
	}
}

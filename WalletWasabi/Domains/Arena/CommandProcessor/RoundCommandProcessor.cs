using System.Collections.Immutable;
using WalletWasabi.Domains.Arena.Aggregates;
using WalletWasabi.Domains.Arena.Command;
using WalletWasabi.Domains.Arena.Events;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Domains.Arena.CommandProcessor
{
	public class RoundCommandProcessor : ICommandProcessor
	{
		public Result Process(StartRoundCommand command, RoundState2 state)
		{
			var errors = PrepareErrors();
			if (!IsStateValid(Phase.New, state, command.GetType().Name, out var errorResult))
			{
				return errorResult;
			}
			return errors.Count > 0 ?
				Result.Fail(errors) :
				Result.Succeed(
					new[] { new RoundStartedEvent(command.RoundParameters) });
		}

		public Result Process(RegisterInputCommand command, RoundState2 state)
		{
			var errors = PrepareErrors();
			if (!IsStateValid(Phase.InputRegistration, state, command.GetType().Name, out var errorResult))
			{
				return errorResult;
			}
			return errors.Count > 0 ?
				Result.Fail(errors) :
				Result.Succeed(
					new[] { new InputRegisteredEvent(command.AliceSecret, command.Coin, command.OwnershipProof) });
		}

		public Result Process(EndRoundCommand command, RoundState2 state)
		{
			return Result.Succeed(new RoundEndedEvent());
		}

		public Result Process(ConfirmInputConnectionCommand command, RoundState2 state)
		{
			return Result.Succeed(new InputConnectionConfirmedEvent(command.Coin, command.OwnershipProof));
		}

		public Result Process(RemoveInputCommand command, RoundState2 state)
		{
			return Result.Succeed(new InputUnregistered(command.AliceOutPoint));
		}

		public Result Process(RegisterOutputCommand command, RoundState2 state)
		{
			return Result.Succeed(new OutputRegisteredEvent(command.Script, command.Value));
		}

		public Result Process(StartOutputRegistrationCommand command, RoundState2 state)
		{
			return Result.Succeed(new OutputRegistrationStartedEvent());
		}

		public Result Process(StartConnectionConfirmationCommand command, RoundState2 state)
		{
			return Result.Succeed(new InputsConnectionConfirmationStartedEvent());
		}

		public Result Process(StartTransactionSigningCommand command, RoundState2 state)
		{
			return Result.Succeed(new SigningStartedEvent());
		}

		public Result Process(SucceedRoundCommand command, RoundState2 state)
		{
			return Result.Succeed(new IEvent[] { new RoundSucceedEvent(), new RoundEndedEvent() });
		}

		public Result Process(NotifyInputReadyToSignCommand command, RoundState2 state)
		{
			return Result.Succeed(new InputReadyToSignEvent(command.AliceOutPoint));
		}

		public Result Process(AddSignatureCommand command, RoundState2 state)
		{
			return Result.Succeed(new SignatureAddedEvent(command.AliceOutPoint, command.WitScript));
		}

		public Result Process(ICommand command, IState state)
		{
			if (state is not RoundState2 roundState)
				throw new ArgumentException($"State should be type of {nameof(RoundState2)}.", nameof(state));
			return ProcessDynamic(command, roundState);
		}

		protected Result ProcessDynamic(dynamic command, RoundState2 state)
		{
			return Process(command, state);
		}

		private static ImmutableArray<IError>.Builder PrepareErrors()
		{
			return ImmutableArray.CreateBuilder<IError>();
		}

		private bool IsStateValid(Phase expected, RoundState2 state, string commandName, out Result errorResult)
		{
			var isStateValid = expected == state.Phase;
			errorResult = null!;
			if (!isStateValid)
			{
				errorResult = Result.Fail(
					new Error(
						$"Unexpected State for '{commandName}'. expected: '{expected}', actual: '{state.Phase}'"));
			}
			return isStateValid;
		}
	}
}

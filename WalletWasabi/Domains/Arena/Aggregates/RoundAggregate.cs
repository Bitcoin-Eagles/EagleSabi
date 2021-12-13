using WalletWasabi.Domains.Arena.Events;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Domains.Arena.Aggregates
{
	public class RoundAggregate : IAggregate
	{
		public RoundState2 State { get; private set; } = new();

		IState IAggregate.State => State;

		public void Apply(IEvent ev)
		{
			ApplyDynamic(ev);
		}

		protected void ApplyDynamic(dynamic ev)
		{
			Apply(ev);
		}

		public void Apply(RoundStartedEvent ev)
		{
			State = State with { RoundParameters = ev.RoundParameters, Phase = Phase.InputRegistration };
		}

		public void Apply(InputRegisteredEvent ev)
		{
			State = State with { Inputs = State.Inputs.Add(new InputState(ev.Coin, ev.OwnershipProof, ev.AliceSecret)) };
		}

		public void Apply(InputUnregistered ev)
		{
			State = State with { Inputs = State.Inputs.RemoveAll(input => input.Coin.Outpoint == ev.AliceOutPoint) };
		}

		public void Apply(InputsConnectionConfirmationStartedEvent _)
		{
			State = State with { Phase = Phase.ConnectionConfirmation };
		}

		public void Apply(InputConnectionConfirmedEvent ev)
		{
			var index = State.Inputs.FindIndex(input => input.Coin.Outpoint == ev.Coin.Outpoint);
			if (index < 0)
			{
				// On client side we have to add the input here because InputRegisteredEvent not sent to clients.
				State = State with { Inputs = State.Inputs.Add(new InputState(ev.Coin, ev.OwnershipProof, ConnectionConfirmed: true)) };
				return;
			}

			var newState = State.Inputs[index] with
			{
				Coin = ev.Coin,
				OwnershipProof = ev.OwnershipProof,
				ConnectionConfirmed = true
			};

			State = State with { Inputs = State.Inputs.SetItem(index, newState) };
		}

		public void Apply(OutputRegistrationStartedEvent _)
		{
			State = State with { Phase = Phase.OutputRegistration };
		}

		public void Apply(OutputRegisteredEvent ev)
		{
			State = State with { Outputs = State.Outputs.Add(new OutputState(ev.Script, ev.CredentialAmount)) };
		}

		public void Apply(InputReadyToSignEvent ev)
		{
			var index = State.Inputs.FindIndex(input => input.Coin.Outpoint == ev.AliceOutPoint);
			if (index < 0)
			{
				return;
			}

			State = State with { Inputs = State.Inputs.SetItem(index, State.Inputs[index] with { ReadyToSign = true }) };
		}

		public void Apply(SigningStartedEvent _)
		{
			State = State with { Phase = Phase.TransactionSigning };
		}

		public void Apply(SignatureAddedEvent ev)
		{
			var index = State.Inputs.FindIndex(input => input.Coin.Outpoint == ev.AliceOutPoint);
			if (index < 0)
			{
				return;
			}

			State = State with { Inputs = State.Inputs.SetItem(index, State.Inputs[index] with { WitScript = ev.WitScript }) };
		}

		public void Apply(RoundEndedEvent _)
		{
			State = State with { Phase = Phase.Ended };
		}

		public void Apply(RoundSucceedEvent _)
		{
			State = State with { Succeeded = true };
		}
	}
}

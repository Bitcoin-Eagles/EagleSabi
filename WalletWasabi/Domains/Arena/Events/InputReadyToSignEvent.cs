using NBitcoin;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.Domains.Arena.Events
{
	public record InputReadyToSignEvent(OutPoint AliceOutPoint) : IEvent;
}

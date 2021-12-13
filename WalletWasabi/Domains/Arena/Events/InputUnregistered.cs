using NBitcoin;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.Domains.Arena.Events
{
	public record InputUnregistered(OutPoint AliceOutPoint) : IEvent;
}

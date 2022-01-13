using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Domains.Arena.ReadModels
{
	public record ActiveRoundInfo(Phase Phase, long SequenceId, uint256? BlameOf);
}

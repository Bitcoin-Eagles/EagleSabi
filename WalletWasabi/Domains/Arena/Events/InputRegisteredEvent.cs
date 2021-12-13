using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Domains.Arena.Events
{
	public record InputRegisteredEvent(Guid AliceSecret, Coin Coin, OwnershipProof OwnershipProof) : IEvent;
}

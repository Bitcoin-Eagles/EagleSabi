using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Domains.Arena.Interfaces;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.Domains.Arena.Events
{
	public record RoundSucceedEvent() : IEvent, IRoundClientEvent;
}

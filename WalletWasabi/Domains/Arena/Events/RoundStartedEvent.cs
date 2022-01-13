using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Domains.Arena.Aggregates;
using WalletWasabi.Domains.Arena.Interfaces;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Domains.Arena.Events
{
	public record RoundStartedEvent(RoundParameters2 RoundParameters) : IEvent, IRoundClientEvent;
}

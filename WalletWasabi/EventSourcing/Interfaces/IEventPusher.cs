using System.Threading.Tasks;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public interface IEventPusher
	{
		Task PushAsync();
	}
}

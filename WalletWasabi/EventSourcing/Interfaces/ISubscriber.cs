using System.Threading.Tasks;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public interface ISubscriber<TMessage>
	{
		Task Receive(TMessage message);
	}
}

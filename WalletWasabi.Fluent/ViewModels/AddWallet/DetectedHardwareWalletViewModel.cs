using ReactiveUI;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{

	public class DetectedHardwareWalletViewModel : RoutableViewModel
	{
		public DetectedHardwareWalletViewModel(HardwareDetectionState detectionState)
		{
			var type = WalletType.Hardware;

			switch (detectionState.SelectedDevice!.Model)
			{
				case Hwi.Models.HardwareWalletModels.Coldcard:
				case Hwi.Models.HardwareWalletModels.Coldcard_Simulator:
					type = WalletType.Coldcard;
					break;

				case Hwi.Models.HardwareWalletModels.Ledger_Nano_S:
					type = WalletType.Ledger;
					break;

				case Hwi.Models.HardwareWalletModels.Trezor_1:
				case Hwi.Models.HardwareWalletModels.Trezor_1_Simulator:
				case Hwi.Models.HardwareWalletModels.Trezor_T:
				case Hwi.Models.HardwareWalletModels.Trezor_T_Simulator:
					type = WalletType.Trezor;
					break;
			}

			Type = type;
			TypeName = detectionState.SelectedDevice.Model.FriendlyName();

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				IsBusy = true;

				var newWallet = await Task.Run(async () =>
				{
					return await detectionState.GenerateWalletAsync();
				});

				detectionState.WalletManager.AddWallet(newWallet);

				Navigate().To(new AddedWalletPageViewModel(detectionState.WalletName, Type));

				IsBusy = false;
			});

			NoCommand = ReactiveCommand.Create(() =>
			{
				Navigate().To(new ConnectHardwareWalletViewModel(detectionState.WalletName, detectionState.Network, detectionState.WalletManager, false));
			});
		}

		public WalletType Type { get; }

		public string TypeName { get; }

		public ICommand NoCommand { get; }
	}
}

using NBitcoin;
using ReactiveUI;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class ConnectHardwareWalletViewModel : RoutableViewModel
	{
		private readonly bool _trySkipPage;
		private readonly HardwareDetectionState _detectionState;

		public ConnectHardwareWalletViewModel(string walletName, Network network, WalletManager walletManager, bool trySkipPage = true)
		{
			_detectionState = new HardwareDetectionState(walletName, walletManager, network);

			_trySkipPage = trySkipPage;

			if (trySkipPage)
			{
				IsBusy = true;
			}

			NextCommand = ReactiveCommand.Create(() =>
			{
				Navigate().To(new DetectHardwareWalletViewModel(_detectionState));
			});
		}

		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			if (!inStack && _trySkipPage)
			{
				RxApp.MainThreadScheduler.Schedule(async () =>
				{
					await _detectionState.EnumerateHardwareWalletsAsync(CancellationToken.None);

					var deviceCount = _detectionState.Devices.Count();

					if (deviceCount == 0)
					{
						// navigate to detecting page.
						Navigate().To(new DetectHardwareWalletViewModel(_detectionState));
					}
					else if (deviceCount == 1)
					{
						// navigate to detected hw wallet page.
						_detectionState.SelectedDevice = _detectionState.Devices.First();

						Navigate().To(new DetectedHardwareWalletViewModel(_detectionState));
					}
					else
					{
						// Do nothing... stay on this page.
					}

					IsBusy = false;
				});
			}
		}
	}
}

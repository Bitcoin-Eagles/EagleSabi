using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Navigation;
using System.Windows.Input;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class WalletViewModel : WalletViewModelBase
	{
		[AutoNotify] private IList<TileViewModel> _tiles;

		protected WalletViewModel(
			WalletManager walletManager,
			TransactionBroadcaster transactionBroadcaster,
			Config config,
			UiConfig uiConfig,
			HttpClientFactory clientFactory,
			Wallet wallet)
			: base(wallet)
		{
			Disposables = Disposables is null
				? new CompositeDisposable()
				: throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			var balanceChanged =
				Observable.FromEventPattern(
						Wallet.TransactionProcessor,
						nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
					.Select(_ => Unit.Default)
					.Merge(Observable.FromEventPattern(Wallet, nameof(Wallet.NewFilterProcessed))
						.Select(_ => Unit.Default))
					.Merge(uiConfig.WhenAnyValue(x => x.PrivacyMode).Select(_ => Unit.Default))
					.Merge(Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Select(_ => Unit.Default))
					.Throttle(TimeSpan.FromSeconds(0.1))
					.ObserveOn(RxApp.MainThreadScheduler);

			History = new HistoryViewModel(this, uiConfig, balanceChanged);

			BalanceTile = new WalletBalanceTileViewModel(wallet, balanceChanged)
			{
				ColumnSpan = new List<int> { 1, 1, 1 },
				RowSpan = new List<int> { 1, 1, 1 }
			};
			RoundStatusTile = new RoundStatusTileViewModel(wallet)
			{
				ColumnSpan = new List<int> { 1, 1, 1 },
				RowSpan = new List<int> { 1, 1, 1 }
			};
			BtcPriceTile = new BtcPriceTileViewModel(wallet)
			{
				ColumnSpan = new List<int> { 1, 1, 1 },
				RowSpan = new List<int> { 1, 1, 1 }
			};
			WalletPieChart = new WalletPieChartTileViewModel(wallet, balanceChanged)
			{
				ColumnSpan = new List<int> { 1, 1, 1 },
				RowSpan = new List<int> { 1, 2, 2 }
			};
			BalanceChartTile = new WalletBalanceChartTileViewModel(History.UnfilteredTransactions)
			{
				ColumnSpan = new List<int> { 2, 2, 2 },
				RowSpan = new List<int> { 1, 2, 2 }
			};

			_tiles = new List<TileViewModel>
			{
				BalanceTile,
				RoundStatusTile,
				BtcPriceTile,
				WalletPieChart,
				BalanceChartTile
			};

			SendCommand = ReactiveCommand.Create(() =>
			{
				Navigate(NavigationTarget.DialogScreen)
					.To(new SendViewModel(this, transactionBroadcaster, config, uiConfig, clientFactory));
			});

			ReceiveCommand = ReactiveCommand.Create(() =>
			{
				Navigate(NavigationTarget.DialogScreen)
					.To(new ReceiveViewModel(this, walletManager, wallet.BitcoinStore, uiConfig));
			});
		}

		public ICommand SendCommand { get; }

		public ICommand ReceiveCommand { get; }

		private CompositeDisposable Disposables { get; set; }

		public override string IconName => "web_asset_regular";

		public HistoryViewModel History { get; }

		public WalletBalanceTileViewModel BalanceTile { get; }

		public RoundStatusTileViewModel RoundStatusTile { get; }

		public BtcPriceTileViewModel BtcPriceTile { get; }

		public WalletPieChartTileViewModel WalletPieChart { get; }

		public WalletBalanceChartTileViewModel BalanceChartTile { get; }

		public void NavigateAndHighlight(uint256 txid)
		{
			Navigate().To(this, NavigationMode.Clear);

			RxApp.MainThreadScheduler.Schedule(async () =>
			{
				await Task.Delay(500);
				History.SelectTransaction(txid);
			});
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			foreach (var tile in _tiles)
			{
				tile.Activate(disposables);
			}

			History.Activate(disposables);
		}

		public static WalletViewModel Create(
			WalletManager walletManager,
			TransactionBroadcaster broadcaster,
			Config config,
			UiConfig uiConfig,
			HttpClientFactory httpClientFactory,
			Wallet wallet)
		{
			return wallet.KeyManager.IsHardwareWallet
				? new HardwareWalletViewModel(
					walletManager,
					broadcaster,
					config,
					uiConfig,
					httpClientFactory,
					wallet)
				: wallet.KeyManager.IsWatchOnly
					? new WatchOnlyWalletViewModel(
						walletManager,
						broadcaster,
						config,
						uiConfig,
						httpClientFactory,
						wallet)
					: new WalletViewModel(
						walletManager,
						broadcaster,
						config,
						uiConfig,
						httpClientFactory,
						wallet);
		}
	}
}

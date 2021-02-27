﻿using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization
{
	public abstract class AuthorizationDialogBase : DialogViewModelBase<SmartTransaction?>
	{
		protected AuthorizationDialogBase()
		{
			var canExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

			BackCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Back), canExecute);
			CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel), canExecute);
			NextCommand = ReactiveCommand.CreateFromTask(Authorize, canExecute);

			EnableAutoBusyOn(NextCommand);
		}

		protected abstract Task Authorize();
	}
}

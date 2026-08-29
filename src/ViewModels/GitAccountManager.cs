using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class GitAccountManager : ObservableObject
    {
        public AvaloniaList<Models.GitAccount> Accounts => GitAccountStore.Instance.Accounts;

        public Models.GitAccount SelectedAccount
        {
            get => _selectedAccount;
            set => SetProperty(ref _selectedAccount, value);
        }

        public void AddAccount()
        {
            var account = new Models.GitAccount
            {
                Name = "New Account",
            };

            Accounts.Add(account);
            SelectedAccount = account;
        }

        public void RemoveSelectedAccount()
        {
            if (_selectedAccount == null)
                return;

            Accounts.Remove(_selectedAccount);
            SelectedAccount = null;
        }

        public void Save()
        {
            GitAccountStore.Instance.Save();
        }

        private Models.GitAccount _selectedAccount = null;
    }
}

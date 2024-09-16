using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class AIAssistant : ChromelessWindow
    {
        public AIAssistant()
        {
            _cancel = new CancellationTokenSource();
            InitializeComponent();
            ProgressMessage.Text = "Generating commit message... Please wait!";
        }

        public void GenerateCommitMessage()
        {
            if (DataContext is ViewModels.WorkingCopy vm)
            {
                Task.Run(() =>
                {
                    var message = new Commands.GenerateCommitMessage(vm.RepoPath, vm.Staged, _cancel.Token, SetDescription).Result();
                    if (_cancel.IsCancellationRequested)
                        return;

                    Dispatcher.UIThread.Invoke(() =>
                    {
                        if (DataContext is ViewModels.WorkingCopy wc)
                            wc.CommitMessage = message;

                        Close();
                    });
                }, _cancel.Token);
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            _cancel.Cancel();
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void SetDescription(string message)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                ProgressMessage.Text = message;
            });
        }

        private CancellationTokenSource _cancel;
    }
}

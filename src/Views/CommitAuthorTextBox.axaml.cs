using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace SourceGit.Views
{
    public partial class CommitAuthorTextBox : UserControl
    {

        public static readonly StyledProperty<string> UserNameProperty =
            AvaloniaProperty.Register<CommitAuthorTextBox, string>(
                nameof(UserName),
                string.Empty,
                defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<string> UserEmailProperty =
            AvaloniaProperty.Register<CommitAuthorTextBox, string>(
                nameof(UserEmail),
                string.Empty,
                defaultBindingMode: BindingMode.TwoWay);

        public string UserName
        {
            get => GetValue(UserNameProperty);
            set => SetValue(UserNameProperty, value);
        }

        public string UserEmail
        {
            get => GetValue(UserEmailProperty);
            set => SetValue(UserEmailProperty, value);
        }

        public CommitAuthorTextBox()
        {
            InitializeComponent();
        }

    }
}

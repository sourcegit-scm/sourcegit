using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace DevBoard.Views
{
    public sealed class DevSpaceProfileManager : Window
    {
        public DevSpaceProfileManager()
        {
            Title = "DevSpace Terminal Profiles";
            Width = 720;
            Height = 610;
            MinWidth = 620;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            BuildDefaultTerminalPicker();
            BuildIconPicker();
            BuildContent();
            RefreshProfiles();
        }

        private void BuildDefaultTerminalPicker()
        {
            foreach (var choice in DevBoard.DevSpaces.DevSpaceProfileSettings.SupportedTerminals)
            {
                var item = new ComboBoxItem
                {
                    Content = choice.Name,
                    Tag = choice.Value,
                };
                _defaultTerminal.Items.Add(item);
                if (string.Equals(
                    choice.Value,
                    DevBoard.DevSpaces.DevSpaceProfileSettings.Instance.DefaultTerminal,
                    StringComparison.OrdinalIgnoreCase))
                {
                    _defaultTerminal.SelectedItem = item;
                }
            }

            if (_defaultTerminal.SelectedIndex < 0 && _defaultTerminal.ItemCount > 0)
                _defaultTerminal.SelectedIndex = 0;
        }

        private void BuildIconPicker()
        {
            foreach (var choice in DevBoard.DevSpaces.DevSpaceProfileSettings.ProfileIcons)
            {
                var icon = new Button
                {
                    Content = choice.Icon,
                    Tag = choice.Icon,
                    FontSize = 20,
                    MinWidth = 36,
                    MinHeight = 36,
                    Padding = new Thickness(4),
                    Margin = new Thickness(0, 0, 6, 6),
                };
                ToolTip.SetTip(icon, choice.Name);
                icon.Click += (_, _) => _icon.Text = choice.Icon;
                _iconPicker.Children.Add(icon);
            }

            _icon.Text = DevBoard.DevSpaces.DevSpaceProfileSettings.DefaultProfileIcon;
        }

        private void BuildContent()
        {
            var root = new Grid
            {
                Margin = new Thickness(16),
                RowDefinitions = new RowDefinitions("Auto,12,*,12,Auto"),
            };

            var terminalRow = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,12,*"),
            };
            terminalRow.Children.Add(new TextBlock
            {
                Text = "Default terminal",
                VerticalAlignment = VerticalAlignment.Center,
            });
            Grid.SetColumn(_defaultTerminal, 2);
            terminalRow.Children.Add(_defaultTerminal);
            root.Children.Add(terminalRow);

            var body = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("220,12,*"),
            };
            Grid.SetRow(body, 2);
            root.Children.Add(body);

            var left = new Grid
            {
                RowDefinitions = new RowDefinitions("*,8,Auto"),
            };
            left.Children.Add(_profiles);

            var profileButtons = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,6,*,6,*"),
            };
            Grid.SetRow(profileButtons, 2);
            left.Children.Add(profileButtons);

            var add = new Button { Content = "Add", HorizontalAlignment = HorizontalAlignment.Stretch };
            add.Click += (_, _) => AddProfile();
            profileButtons.Children.Add(add);

            var duplicate = new Button { Content = "Duplicate", HorizontalAlignment = HorizontalAlignment.Stretch };
            duplicate.Click += (_, _) => DuplicateProfile();
            Grid.SetColumn(duplicate, 2);
            profileButtons.Children.Add(duplicate);

            var delete = new Button { Content = "Delete", HorizontalAlignment = HorizontalAlignment.Stretch };
            delete.Click += (_, _) => DeleteProfile();
            Grid.SetColumn(delete, 4);
            profileButtons.Children.Add(delete);

            body.Children.Add(left);

            var editor = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,6,Auto,6,Auto,8,Auto,12,Auto,6,Auto,6,Auto,6,Auto,12,Auto,*"),
            };
            Grid.SetColumn(editor, 2);
            body.Children.Add(editor);

            editor.Children.Add(new TextBlock { Text = "Icon" });
            Grid.SetRow(_icon, 2);
            editor.Children.Add(_icon);

            var iconHint = new TextBlock
            {
                Text = OperatingSystem.IsWindows()
                    ? "Press Win + . to choose any emoji, or use a quick pick below."
                    : "Enter any emoji, or use a quick pick below.",
                Opacity = 0.7,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            };
            Grid.SetRow(iconHint, 4);
            editor.Children.Add(iconHint);

            Grid.SetRow(_iconPicker, 6);
            editor.Children.Add(_iconPicker);

            var nameLabel = new TextBlock { Text = "Name" };
            Grid.SetRow(nameLabel, 8);
            editor.Children.Add(nameLabel);
            Grid.SetRow(_name, 10);
            editor.Children.Add(_name);

            var pathLabel = new TextBlock { Text = "Workspace-relative path" };
            Grid.SetRow(pathLabel, 12);
            editor.Children.Add(pathLabel);
            Grid.SetRow(_path, 14);
            editor.Children.Add(_path);

            var commandLabel = new TextBlock { Text = "Startup command" };
            Grid.SetRow(commandLabel, 16);
            editor.Children.Add(commandLabel);
            Grid.SetRow(_command, 17);
            editor.Children.Add(_command);

            _profiles.SelectionChanged += (_, _) => LoadSelectedProfile();

            var footer = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,6,Auto,*,Auto,6,Auto"),
            };
            Grid.SetRow(footer, 4);
            root.Children.Add(footer);

            var import = new Button { Content = "Import JSON" };
            import.Click += async (_, _) => await ImportAsync();
            footer.Children.Add(import);

            var export = new Button { Content = "Export JSON" };
            export.Click += async (_, _) => await ExportAsync();
            Grid.SetColumn(export, 2);
            footer.Children.Add(export);

            var apply = new Button { Content = "Apply Profile" };
            apply.Click += async (_, _) => await ApplyProfileAsync();
            Grid.SetColumn(apply, 4);
            footer.Children.Add(apply);

            var done = new Button { Content = "Done" };
            done.Click += async (_, _) => await SaveAndCloseAsync();
            Grid.SetColumn(done, 6);
            footer.Children.Add(done);

            Content = root;
        }

        private void AddProfile()
        {
            var profile = new DevBoard.DevSpaces.DevSpaceTerminalProfile
            {
                Name = "New Profile",
                Path = ".",
                Command = string.Empty,
            };
            DevBoard.DevSpaces.DevSpaceProfileSettings.Instance.Profiles.Add(profile);
            RefreshProfiles(profile.Id);
        }

        private void DuplicateProfile()
        {
            var source = SelectedProfile;
            if (source == null)
                return;

            var copy = source.Clone(createNewId: true);
            copy.Name += " Copy";
            DevBoard.DevSpaces.DevSpaceProfileSettings.Instance.Profiles.Add(copy);
            RefreshProfiles(copy.Id);
        }

        private void DeleteProfile()
        {
            var profile = SelectedProfile;
            if (profile == null)
                return;

            DevBoard.DevSpaces.DevSpaceProfileSettings.Instance.Profiles.Remove(profile);
            RefreshProfiles();
        }

        private async Task<bool> ApplyProfileAsync()
        {
            var profile = SelectedProfile;
            if (profile == null)
                return true;

            profile.Name = _name.Text?.Trim() ?? string.Empty;
            profile.Icon = _icon.Text;
            profile.Path = _path.Text?.Trim() ?? string.Empty;
            profile.Command = _command.Text?.Trim() ?? string.Empty;

            try
            {
                DevBoard.DevSpaces.DevSpaceProfileSettings.ValidateProfile(profile);
                _icon.Text = profile.Icon;
                DevBoard.DevSpaces.DevSpaceProfileSettings.Instance.Save();
                RefreshProfiles(profile.Id);
                return true;
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex.Message);
                return false;
            }
        }

        private async Task SaveAndCloseAsync()
        {
            if (!await ApplyProfileAsync())
                return;

            if (_defaultTerminal.SelectedItem is ComboBoxItem { Tag: string terminal })
                DevBoard.DevSpaces.DevSpaceProfileSettings.Instance.DefaultTerminal = terminal;

            DevBoard.DevSpaces.DevSpaceProfileSettings.Instance.Save();
            Close();
        }

        private async Task ImportAsync()
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Import DevSpace Profiles",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("JSON") { Patterns = ["*.json"] },
                    ],
                });
                var file = files.FirstOrDefault();
                if (file == null)
                    return;

                await using var stream = await file.OpenReadAsync();
                await DevBoard.DevSpaces.DevSpaceProfileSettings.Instance.ImportProfilesAsync(stream);
                RefreshProfiles();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex.Message);
            }
        }

        private async Task ExportAsync()
        {
            try
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Export DevSpace Profiles",
                    SuggestedFileName = "devspace-profiles.json",
                    DefaultExtension = "json",
                    FileTypeChoices =
                    [
                        new FilePickerFileType("JSON") { Patterns = ["*.json"] },
                    ],
                });
                if (file == null)
                    return;

                await using var stream = await file.OpenWriteAsync();
                stream.SetLength(0);
                await DevBoard.DevSpaces.DevSpaceProfileSettings.Instance.ExportProfilesAsync(stream);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex.Message);
            }
        }

        private void RefreshProfiles(string selectedId = null)
        {
            var settings = DevBoard.DevSpaces.DevSpaceProfileSettings.Instance;
            _profiles.ItemsSource = null;
            _profiles.ItemsSource = settings.Profiles;

            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                _profiles.SelectedItem = settings.Profiles.FirstOrDefault(x => x.Id == selectedId);
            }
            else if (settings.Profiles.Count > 0)
            {
                _profiles.SelectedIndex = 0;
            }
            else
            {
                ClearEditor();
            }
        }

        private void LoadSelectedProfile()
        {
            var profile = SelectedProfile;
            if (profile == null)
            {
                ClearEditor();
                return;
            }

            _icon.Text = DevBoard.DevSpaces.DevSpaceProfileSettings.NormalizeProfileIcon(profile.Icon);
            _name.Text = profile.Name;
            _path.Text = profile.Path;
            _command.Text = profile.Command;
        }

        private void ClearEditor()
        {
            _icon.Text = DevBoard.DevSpaces.DevSpaceProfileSettings.DefaultProfileIcon;
            _name.Text = string.Empty;
            _path.Text = string.Empty;
            _command.Text = string.Empty;
        }

        private Task ShowErrorAsync(string message)
        {
            return new Alert().ShowAsync(this, message, true);
        }

        private DevBoard.DevSpaces.DevSpaceTerminalProfile SelectedProfile =>
            _profiles.SelectedItem as DevBoard.DevSpaces.DevSpaceTerminalProfile;

        private readonly ComboBox _defaultTerminal = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        private readonly ListBox _profiles = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        private readonly TextBox _icon = new()
        {
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Left,
            Watermark = "🐱",
            FontSize = 22,
        };

        private readonly WrapPanel _iconPicker = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        private readonly TextBox _name = new();
        private readonly TextBox _path = new() { Watermark = ". or src/MyApp" };
        private readonly TextBox _command = new()
        {
            Watermark = "dotnet run",
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };
    }
}

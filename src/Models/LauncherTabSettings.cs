using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace DevBoard.Models
{
    public enum LauncherTabLayout
    {
        Horizontal = 0,
        Vertical = 1,
    }

    public class LauncherTabSettings : ObservableObject
    {
        [JsonIgnore]
        public static LauncherTabSettings Instance => _instance ??= Load();

        public LauncherTabLayout Layout
        {
            get => _layout;
            set
            {
                if (SetProperty(ref _layout, value))
                {
                    OnPropertyChanged(nameof(LayoutIndex));
                    OnPropertyChanged(nameof(IsVertical));
                    Save();
                }
            }
        }

        [JsonIgnore]
        public int LayoutIndex
        {
            get => (int)_layout;
            set => Layout = value == (int)LauncherTabLayout.Vertical ? LauncherTabLayout.Vertical : LauncherTabLayout.Horizontal;
        }

        public double VerticalWidth
        {
            get => _verticalWidth;
            set
            {
                if (SetProperty(ref _verticalWidth, Math.Clamp(value, 160, 420)))
                    Save();
            }
        }

        [JsonIgnore]
        public bool IsVertical => _layout == LauncherTabLayout.Vertical;

        private static LauncherTabSettings Load()
        {
            var path = Path.Combine(Native.OS.DataDir, "launcher_tabs.json");
            LauncherTabSettings settings = null;

            if (File.Exists(path))
            {
                try
                {
                    settings = JsonSerializer.Deserialize<LauncherTabSettings>(File.ReadAllText(path));
                }
                catch
                {
                    // Fall back to defaults when the settings file is unreadable.
                }
            }

            settings ??= new LauncherTabSettings();
            settings._autoSave = true;
            return settings;
        }

        private void Save()
        {
            if (!_autoSave)
                return;

            try
            {
                Directory.CreateDirectory(Native.OS.DataDir);
                var tmp = Path.Combine(Native.OS.DataDir, "launcher_tabs_tmp.json");
                var path = Path.Combine(Native.OS.DataDir, "launcher_tabs.json");
                File.WriteAllText(tmp, JsonSerializer.Serialize(this));
                File.Move(tmp, path, true);
            }
            catch
            {
                // Keep the live setting even if persistence temporarily fails.
            }
        }

        private static LauncherTabSettings _instance = null;
        private bool _autoSave = false;
        private LauncherTabLayout _layout = LauncherTabLayout.Horizontal;
        private double _verticalWidth = 220;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class SSHKeyHelper : ObservableObject
    {
        public AvaloniaList<Models.SSHKeyPair> Keys
        {
            get;
        }

        public Models.SSHKeyPair SelectedKey
        {
            get => _selectedKey;
            set => SetProperty(ref _selectedKey, value);
        }

        public SSHKeyGenerator Generator
        {
            get => _generator;
            private set => SetProperty(ref _generator, value);
        }

        public SSHKeyHelper()
        {
            _baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
            Keys = new AvaloniaList<Models.SSHKeyPair>();

            Task.Run(() =>
            {
                var sshDir = new DirectoryInfo(_baseDir);
                var keys = new List<Models.SSHKeyPair>();

                if (sshDir.Exists)
                {
                    var files = sshDir.GetFiles("*.pub");
                    foreach (var file in files)
                    {
                        var privateKeyPath = file.FullName.Substring(0, file.FullName.Length - 4);
                        if (File.Exists(privateKeyPath))
                            keys.Add(new(privateKeyPath));
                    }

                    keys.Sort((l, r) => l.Name.CompareTo(r.Name));
                }

                if (keys.Count > 0)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Keys.AddRange(keys);
                        SelectedKey = keys[0];
                    });
                }
            });
        }

        public void OpenGenerator()
        {
            Generator = new SSHKeyGenerator(_baseDir);
        }

        public void CloseGenerator()
        {
            Generator = null;
        }

        public void Generate()
        {
            var succ = _generator.Run();
            if (!succ)
                return;

            var keyFile = Path.Combine(_baseDir, $"{_generator.Name}");
            if (File.Exists(keyFile))
            {
                var added = new Models.SSHKeyPair(keyFile);
                Keys.Add(added);
                SelectedKey = added;
            }

            Generator = null;
        }

        public void DeleteSelected()
        {
            var key = SelectedKey;
            if (key == null)
                return;

            try
            {
                if (File.Exists(key.FullPath))
                    File.Delete(key.FullPath);
                if (File.Exists($"{key.FullPath}.pub"))
                    File.Delete($"{key.FullPath}.pub");

                var idx = Keys.IndexOf(key);
                if (idx >= 0)
                {
                    Keys.RemoveAt(idx);

                    if (Keys.Count == 0)
                        SelectedKey = null;
                    else if (idx > Keys.Count - 1)
                        SelectedKey = Keys[Keys.Count - 1];
                    else
                        SelectedKey = Keys[idx];
                }
            }
            catch
            {
                // Ignore any errors during deletion
            }
        }

        private string _baseDir = null;
        private Models.SSHKeyPair _selectedKey = null;
        private SSHKeyGenerator _generator = null;
    }
}

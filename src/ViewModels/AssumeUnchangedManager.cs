﻿using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;

namespace SourceGit.ViewModels
{
    public class AssumeUnchangedManager
    {
        public AvaloniaList<string> Files { get; private set; }

        public AssumeUnchangedManager(string repo)
        {
            _repo = repo;
            Files = new AvaloniaList<string>();

            Task.Run(() =>
            {
                var collect = new Commands.QueryAssumeUnchangedFiles(_repo).Result();
                Dispatcher.UIThread.Invoke(() => Files.AddRange(collect));
            });
        }

        public void Remove(string file)
        {
            if (!string.IsNullOrEmpty(file))
            {
                new Commands.AssumeUnchanged(_repo, file, false).Exec();
                Files.Remove(file);
            }
        }

        private readonly string _repo;
    }
}

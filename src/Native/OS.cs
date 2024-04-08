﻿using System;
using System.Collections.Generic;

using Avalonia;

namespace SourceGit.Native
{
    public static class OS
    {
        public interface IBackend
        {
            void SetupApp(AppBuilder builder);

            string FindGitExecutable();
            
            List<Models.ExternalTerminal> FindExternalTerminals();
            List<Models.ExternalEditor> FindExternalEditors();

            void OpenTerminal(string workdir);
            void OpenInFileManager(string path, bool select);
            void OpenBrowser(string url);
            void OpenWithDefaultEditor(string file);
        }

        public static string GitExecutable { get; set; } = string.Empty;
        public static List<Models.ExternalTerminal> ExternalTerminals { get; set; } = new List<Models.ExternalTerminal>();
        public static List<Models.ExternalEditor> ExternalEditors { get; set; } = new List<Models.ExternalEditor>();

        static OS()
        {
            if (OperatingSystem.IsWindows())
            {
                _backend = new Windows();
            }
            else if (OperatingSystem.IsMacOS())
            {
                _backend = new MacOS();
            }
            else if (OperatingSystem.IsLinux())
            {
                _backend = new Linux();
            }
            else
            {
                throw new Exception("Platform unsupported!!!");
            }

            ExternalTerminals = _backend.FindExternalTerminals();
            ExternalEditors = _backend.FindExternalEditors();
        }

        public static void SetupApp(AppBuilder builder)
        {
            _backend.SetupApp(builder);
        }

        public static string FindGitExecutable()
        {
            return _backend.FindGitExecutable();
        }

        public static void OpenInFileManager(string path, bool select = false)
        {
            _backend.OpenInFileManager(path, select);
        }

        public static void OpenBrowser(string url)
        {
            _backend.OpenBrowser(url);
        }

        public static void OpenTerminal(string workdir)
        {
            _backend.OpenTerminal(workdir);
        }

        public static void OpenWithDefaultEditor(string file)
        {
            _backend.OpenWithDefaultEditor(file);
        }

        private static IBackend _backend = null;
    }
}

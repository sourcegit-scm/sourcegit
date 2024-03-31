using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace SourceGit.Models
{
    public interface IAvatarHost
    {
        void OnAvatarResourceChanged(string md5);
    }

    public static class AvatarManager
    {
        public static string SelectedServer
        {
            get;
            set;
        } = "https://www.gravatar.com/avatar/";

        static AvatarManager()
        {
            _storePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SourceGit", "avatars");
            if (!Directory.Exists(_storePath))
                Directory.CreateDirectory(_storePath);

            Task.Run(() =>
            {
                while (true)
                {
                    var md5 = null as string;

                    lock (_synclock)
                    {
                        foreach (var one in _requesting)
                        {
                            md5 = one;
                            break;
                        }
                    }

                    if (md5 == null)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    var localFile = Path.Combine(_storePath, md5);
                    var img = null as Bitmap;
                    try
                    {
                        var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(2) };
                        var task = client.GetAsync($"{SelectedServer}{md5}?d=404");
                        task.Wait();

                        var rsp = task.Result;
                        if (rsp.IsSuccessStatusCode)
                        {
                            using (var stream = rsp.Content.ReadAsStream())
                            {
                                using (var writer = File.OpenWrite(localFile))
                                {
                                    stream.CopyTo(writer);
                                }
                            }

                            using (var reader = File.OpenRead(localFile))
                            {
                                img = Bitmap.DecodeToWidth(reader, 128);
                            }
                        }
                    }
                    catch { }

                    lock (_synclock)
                    {
                        _requesting.Remove(md5);
                    }

                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_resources.ContainsKey(md5))
                            _resources[md5] = img;
                        else
                            _resources.Add(md5, img);
                        NotifyResourceChanged(md5);
                    });
                }
            });
        }

        public static void Subscribe(IAvatarHost host)
        {
            _avatars.Add(host);
        }

        public static void Unsubscribe(IAvatarHost host)
        {
            _avatars.Remove(host);
        }

        public static Bitmap Request(string md5, bool forceRefetch = false)
        {
            if (forceRefetch)
            {
                if (_resources.ContainsKey(md5))
                    _resources.Remove(md5);

                var localFile = Path.Combine(_storePath, md5);
                if (File.Exists(localFile))
                    File.Delete(localFile);

                NotifyResourceChanged(md5);
            }
            else
            {
                if (_resources.TryGetValue(md5, out var value))
                    return value;

                var localFile = Path.Combine(_storePath, md5);
                if (File.Exists(localFile))
                {
                    try
                    {
                        using (var stream = File.OpenRead(localFile))
                        {
                            var img = Bitmap.DecodeToWidth(stream, 128);
                            _resources.Add(md5, img);
                            return img;
                        }
                    }
                    catch { }
                }
            }

            lock (_synclock)
            {
                if (!_requesting.Contains(md5))
                    _requesting.Add(md5);
            }

            return null;
        }

        private static void NotifyResourceChanged(string md5)
        {
            foreach (var avatar in _avatars)
            {
                avatar.OnAvatarResourceChanged(md5);
            }
        }

        private static readonly object _synclock = new object();
        private static readonly string _storePath = string.Empty;
        private static readonly List<IAvatarHost> _avatars = new List<IAvatarHost>();
        private static readonly Dictionary<string, Bitmap> _resources = new Dictionary<string, Bitmap>();
        private static readonly HashSet<string> _requesting = new HashSet<string>();
    }
}

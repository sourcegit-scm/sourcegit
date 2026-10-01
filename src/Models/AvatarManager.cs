using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace SourceGit.Models
{
    public interface IAvatarHost
    {
        void OnAvatarResourceChanged(string email, Bitmap image);
    }

    public partial class AvatarManager
    {
        public static AvatarManager Instance
        {
            get
            {
                return _instance ??= new AvatarManager();
            }
        }

        private static AvatarManager _instance = null;

        [GeneratedRegex(@"^(?:(\d+)\+)?(.+?)@.+\.github\.com$")]
        private static partial Regex REG_GITHUB_USER_EMAIL();

        private readonly Lock _synclock = new();
        private string _storePath;
        private List<IAvatarHost> _avatars = new List<IAvatarHost>();
        private Cache _cache = new Cache(256);
        private HashSet<string> _requesting = new HashSet<string>();
        private HashSet<string> _defaultAvatars = new HashSet<string>();

        public void Start()
        {
            _storePath = Path.Combine(Native.OS.BasicDirectories.CacheDir, "avatars");
            if (!Directory.Exists(_storePath))
                Directory.CreateDirectory(_storePath);

            LoadDefaultAvatar("noreply@github.com", "github.png");
            LoadDefaultAvatar("unrealbot@epicgames.com", "unreal.png");

            Task.Run(async () =>
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(2);

                while (true)
                {
                    string email = null;

                    lock (_synclock)
                    {
                        foreach (var one in _requesting)
                        {
                            email = one;
                            break;
                        }
                    }

                    if (email == null)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    var md5 = GetEmailHash(email);
                    var matchGitHubUser = REG_GITHUB_USER_EMAIL().Match(email);
                    var url = $"https://www.gravatar.com/avatar/{md5}?d=404";
                    if (matchGitHubUser.Success)
                    {
                        var githubUser = matchGitHubUser.Groups[2].Value;
                        if (githubUser.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase))
                            githubUser = githubUser.Substring(0, githubUser.Length - 5);

                        url = $"https://avatars.githubusercontent.com/{githubUser}";
                    }

                    var localFile = Path.Combine(_storePath, md5);
                    Bitmap img = null;
                    try
                    {
                        var rsp = await client.GetAsync(url);
                        if (rsp.IsSuccessStatusCode)
                        {
                            using (var stream = rsp.Content.ReadAsStream())
                            {
                                using (var writer = File.Create(localFile))
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
                    catch
                    {
                        // ignored
                    }

                    lock (_synclock)
                    {
                        _requesting.Remove(email);
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        _cache.AddOrUpdate(email, img);
                        NotifyResourceChanged(email, img);
                    });
                }

                // ReSharper disable once FunctionNeverReturns
            });
        }

        public void Subscribe(IAvatarHost host)
        {
            _avatars.Add(host);
        }

        public void Unsubscribe(IAvatarHost host)
        {
            _avatars.Remove(host);
        }

        public Bitmap Request(string email, bool forceRefetch)
        {
            if (forceRefetch)
            {
                if (_defaultAvatars.Contains(email))
                    return null;

                _cache.Remove(email);

                var localFile = Path.Combine(_storePath, GetEmailHash(email));
                if (File.Exists(localFile))
                    File.Delete(localFile);

                NotifyResourceChanged(email, null);
            }
            else
            {
                if (_cache.TryGet(email, out var value))
                    return value;

                var localFile = Path.Combine(_storePath, GetEmailHash(email));
                if (File.Exists(localFile))
                {
                    try
                    {
                        using (var stream = File.OpenRead(localFile))
                        {
                            var img = Bitmap.DecodeToWidth(stream, 128);
                            _cache.AddOrUpdate(email, img);
                            return img;
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            lock (_synclock)
            {
                _requesting.Add(email);
            }

            return null;
        }

        public void SetFromLocal(string email, string file)
        {
            try
            {
                Bitmap image;

                using (var stream = File.OpenRead(file))
                {
                    image = Bitmap.DecodeToWidth(stream, 128);
                }

                _cache.AddOrUpdate(email, image);

                lock (_synclock)
                {
                    _requesting.Remove(email);
                }

                var store = Path.Combine(_storePath, GetEmailHash(email));
                File.Copy(file, store, true);
                NotifyResourceChanged(email, image);
            }
            catch
            {
                // ignore
            }
        }

        private void LoadDefaultAvatar(string key, string img)
        {
            var icon = AssetLoader.Open(new Uri($"avares://SourceGit/Resources/Images/{img}", UriKind.RelativeOrAbsolute));
            _cache.AddOrUpdate(key, new Bitmap(icon));
            _defaultAvatars.Add(key);
        }

        private string GetEmailHash(string email)
        {
            var lowered = email.ToLower(CultureInfo.CurrentCulture).Trim();
            var hash = MD5.HashData(Encoding.Default.GetBytes(lowered));
            return Convert.ToHexStringLower(hash);
        }

        private void NotifyResourceChanged(string email, Bitmap image)
        {
            foreach (var avatar in _avatars)
                avatar.OnAvatarResourceChanged(email, image);
        }

        private class Cache
        {
            private readonly int _capacity;
            private readonly LinkedList<Item> _imgList;
            private readonly Dictionary<string, LinkedListNode<Item>> _key2img;

            private class Item
            {
                public string Key { get; }
                public Bitmap Image { get; set; }

                public Item(string key, Bitmap image)
                {
                    Key = key;
                    Image = image;
                }
            }

            public Cache(int capacity)
            {
                _capacity = capacity;
                _key2img = new Dictionary<string, LinkedListNode<Item>>();
                _imgList = new LinkedList<Item>();
            }

            public bool TryGet(string key, out Bitmap bitmap)
            {
                if (_key2img.TryGetValue(key, out var node))
                {
                    _imgList.Remove(node);
                    _imgList.AddFirst(node);
                    bitmap = node.Value.Image;
                    return true;
                }

                bitmap = null;
                return false;
            }

            public void AddOrUpdate(string key, Bitmap bitmap)
            {
                if (_key2img.TryGetValue(key, out var node))
                {
                    _imgList.Remove(node);
                    _imgList.AddFirst(node);
                    node.Value.Image = bitmap;
                    return;
                }

                if (_key2img.Count >= _capacity)
                {
                    var lastNode = _imgList.Last;
                    _imgList.RemoveLast();
                    _key2img.Remove(lastNode.Value.Key);
                }

                var newNode = new LinkedListNode<Item>(new Item(key, bitmap));
                _imgList.AddFirst(newNode);
                _key2img[key] = newNode;
            }

            public void Remove(string key)
            {
                if (_key2img.TryGetValue(key, out var node))
                {
                    _imgList.Remove(node);
                    _key2img.Remove(key);
                }
            }
        }
    }
}

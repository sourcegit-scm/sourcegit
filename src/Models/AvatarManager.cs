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
        private Dictionary<string, Bitmap> _resources = new Dictionary<string, Bitmap>();
        private HashSet<string> _requesting = new HashSet<string>();
        private HashSet<string> _defaultAvatars = new HashSet<string>();

        public void Start()
        {
            _storePath = Path.Combine(Native.OS.DataDir, "avatars");
            if (!Directory.Exists(_storePath))
                Directory.CreateDirectory(_storePath);

            LoadDefaultAvatar("noreply@github.com", "github.png");
            LoadDefaultAvatar("unrealbot@epicgames.com", "unreal.png");

            Task.Run(async () =>
            {
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
                    var url = matchGitHubUser.Success ?
                        $"https://avatars.githubusercontent.com/{matchGitHubUser.Groups[2].Value}" :
                        $"https://www.gravatar.com/avatar/{md5}?d=404";

                    var localFile = Path.Combine(_storePath, md5);
                    Bitmap img = null;
                    try
                    {
                        using var client = new HttpClient();
                        client.Timeout = TimeSpan.FromSeconds(2);
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
                        _resources[email] = img;
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

                _resources.Remove(email);

                var localFile = Path.Combine(_storePath, GetEmailHash(email));
                if (File.Exists(localFile))
                    File.Delete(localFile);

                NotifyResourceChanged(email, null);
            }
            else
            {
                if (_resources.TryGetValue(email, out var value))
                    return value;

                var localFile = Path.Combine(_storePath, GetEmailHash(email));
                if (File.Exists(localFile))
                {
                    try
                    {
                        using (var stream = File.OpenRead(localFile))
                        {
                            var img = Bitmap.DecodeToWidth(stream, 128);
                            _resources.Add(email, img);
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

                _resources[email] = image;

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
            _resources.Add(key, new Bitmap(icon));
            _defaultAvatars.Add(key);
        }

        private string GetEmailHash(string email)
        {
            var lowered = email.ToLower(CultureInfo.CurrentCulture).Trim();
            var hash = MD5.HashData(Encoding.Default.GetBytes(lowered));
            var builder = new StringBuilder(hash.Length * 2);
            foreach (var c in hash)
                builder.Append(c.ToString("x2"));
            return builder.ToString();
        }

        private void NotifyResourceChanged(string email, Bitmap image)
        {
            foreach (var avatar in _avatars)
                avatar.OnAvatarResourceChanged(email, image);
        }
    }
}

using System.Threading.Tasks;

using Avalonia.Media.Imaging;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class LFSImageDiff : ObservableObject
    {
        public Models.LFSDiff LFS
        {
            get;
        }

        public Models.ImageDiff Image
        {
            get => _image;
            private set => SetProperty(ref _image, value);
        }

        public LFSImageDiff(string repo, Models.LFSDiff lfs)
        {
            LFS = lfs;

            Task.Run(() =>
            {
                var img = new Models.ImageDiff();
                (img.Old, img.OldFileSize) = BitmapFromLFSObject(repo, lfs.Old);
                (img.New, img.NewFileSize) = BitmapFromLFSObject(repo, lfs.New);

                Dispatcher.UIThread.Invoke(() => Image = img);
            });
        }

        public static (Bitmap, long) BitmapFromLFSObject(string repo, Models.LFSObject lfs)
        {
            if (string.IsNullOrEmpty(lfs.Oid) || lfs.Size == 0)
                return (null, 0);

            var stream = Commands.QueryFileContent.FromLFS(repo, lfs.Oid, lfs.Size);
            var size = stream.Length;
            return size > 0 ? (new Bitmap(stream), size) : (null, size);
        }

        private Models.ImageDiff _image;
    }
}

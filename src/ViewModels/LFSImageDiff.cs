using System.Threading.Tasks;
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

        public LFSImageDiff(string repo, Models.LFSDiff lfs, Models.ImageDecoder decoder)
        {
            LFS = lfs;

            Task.Run(() =>
            {
                var oldImage = ImageSource.FromLFSObject(repo, lfs.Old, decoder);
                var newImage = ImageSource.FromLFSObject(repo, lfs.New, decoder);

                var img = new Models.ImageDiff()
                {
                    Old = oldImage.Bitmap,
                    OldFileSize = oldImage.Size,
                    New = newImage.Bitmap,
                    NewFileSize = newImage.Size
                };

                Dispatcher.UIThread.Invoke(() => Image = img);
            });
        }

        private Models.ImageDiff _image;
    }
}

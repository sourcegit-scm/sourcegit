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

            Task.Run(async () =>
            {
                var oldImage = await ImageSource.FromLFSObjectAsync(repo, lfs.Old, decoder).ConfigureAwait(false);
                var newImage = await ImageSource.FromLFSObjectAsync(repo, lfs.New, decoder).ConfigureAwait(false);

                var img = new Models.ImageDiff()
                {
                    Old = oldImage.Bitmap,
                    OldFileSize = oldImage.Size,
                    New = newImage.Bitmap,
                    NewFileSize = newImage.Size
                };

                Dispatcher.UIThread.Post(() => Image = img);
            });
        }

        private Models.ImageDiff _image;
    }
}

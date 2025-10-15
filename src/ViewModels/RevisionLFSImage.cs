using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class RevisionLFSImage : ObservableObject
    {
        public Models.RevisionLFSObject LFS
        {
            get;
        }

        public Models.RevisionImageFile Image
        {
            get => _image;
            private set => SetProperty(ref _image, value);
        }

        public RevisionLFSImage(string repo, string file, Models.LFSObject lfs, Models.ImageDecoder decoder)
        {
            LFS = new Models.RevisionLFSObject() { Object = lfs };

            Task.Run(async () =>
            {
                var source = await ImageSource.FromLFSObjectAsync(repo, lfs, decoder).ConfigureAwait(false);
                var img = new Models.RevisionImageFile(file, source.Bitmap, source.Size);
                Dispatcher.UIThread.Post(() => Image = img);
            });
        }

        private Models.RevisionImageFile _image = null;
    }
}

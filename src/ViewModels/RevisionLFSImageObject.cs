using System.Threading.Tasks;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class RevisionLFSImageObject : ObservableObject
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

        public RevisionLFSImageObject(string repo, Models.RevisionLFSObject lfs, string ext)
        {
            LFS = lfs;
            Task.Run(() =>
            {
                var img = new Models.RevisionImageFile
                {
                    ImageType = ext
                };
                (img.Image, img.FileSize) = SourceGit.ViewModels.LFSImageDiff.BitmapFromLFSObject(repo, LFS.Object);
                Dispatcher.UIThread.Invoke(() => Image = img);
            });
        }

        private Models.RevisionImageFile _image;
    }
}

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class ICommandPalette : ObservableObject, IDisposable
    {
        public void Dispose()
        {
            Cleanup();
        }

        public virtual void Cleanup()
        {
        }
    }
}

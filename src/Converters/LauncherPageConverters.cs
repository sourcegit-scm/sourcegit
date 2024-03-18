using System.Collections.Generic;

using Avalonia.Collections;
using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class LauncherPageConverters
    {
        public static FuncMultiValueConverter<object, bool> ToTabSeperatorVisible =
            new FuncMultiValueConverter<object, bool>(v =>
            {
                if (v == null) return false;

                var array = new List<object>();
                array.AddRange(v);
                if (array.Count != 3) return false;

                var self = array[0] as ViewModels.LauncherPage;
                if (self == null) return false;

                var selected = array[1] as ViewModels.LauncherPage;
                var collections = array[2] as AvaloniaList<ViewModels.LauncherPage>;

                if (selected != null && collections != null && (self == selected || collections.IndexOf(self) + 1 == collections.IndexOf(selected)))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            });
    }
}
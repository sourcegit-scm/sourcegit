using System;

using Avalonia.Styling;

namespace SourceGit.Models
{
    public static class AppThemes
    {
        public static readonly ThemeVariant Monokai = new("Monokai", ThemeVariant.Dark);

        public static ThemeVariant FromName(string theme)
        {
            if (string.IsNullOrEmpty(theme))
                return ThemeVariant.Default;

            if (theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
                return ThemeVariant.Light;

            if (theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                return ThemeVariant.Dark;

            if (theme.Equals("Monokai", StringComparison.OrdinalIgnoreCase))
                return Monokai;

            return ThemeVariant.Default;
        }

        public static bool IsDark(ThemeVariant variant)
        {
            while (variant != null)
            {
                if (variant == ThemeVariant.Dark)
                    return true;

                if (variant == ThemeVariant.Light)
                    return false;

                variant = variant.InheritVariant;
            }

            return false;
        }

        public static bool IsMonokai(ThemeVariant variant)
        {
            return variant == Monokai;
        }
    }
}

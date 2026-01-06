using System;
using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace SourceGit
{
    public static class StringExtensions
    {
        public static string Quoted(this string value)
        {
            return $"\"{Escaped(value)}\"";
        }

        public static string Escaped(this string value)
        {
            return value.Replace("\"", "\\\"", StringComparison.Ordinal);
        }
    }

    public static class CommandExtensions
    {
        public static T Use<T>(this T cmd, Models.ICommandLog log) where T : Commands.Command
        {
            cmd.Log = log;
            return cmd;
        }
    }

    public class DataGridExtension
    {
        public static readonly AttachedProperty<IList> SelectedItemsProperty =
            AvaloniaProperty.RegisterAttached<DataGridExtension, DataGrid, IList>("SelectedItems");

        public static void SetSelectedItems(DataGrid obj, IList value) => obj.SetValue(SelectedItemsProperty, value);
        public static IList GetSelectedItems(DataGrid obj) => obj.GetValue(SelectedItemsProperty);


        public static readonly AttachedProperty<bool> IsUpdatingSelectedItemsProperty =
            AvaloniaProperty.RegisterAttached<DataGridExtension, DataGrid, bool>("IsUpdatingSelectedItems");

        public static void SetIsUpdatingSelectedItems(DataGrid obj, bool value) =>
            obj.SetValue(IsUpdatingSelectedItemsProperty, value);

        public static bool GetIsUpdatingSelectedItems(DataGrid obj) => obj.GetValue(IsUpdatingSelectedItemsProperty);

        static DataGridExtension()
        {
            SelectedItemsProperty.Changed.AddClassHandler((DataGrid target,
                AvaloniaPropertyChangedEventArgs<IList> args) =>
            {
                SetIsUpdatingSelectedItems(target, true);
                target.SelectedItems.Clear();
                var newItems = args.GetNewValue<IList>();
                if (newItems != null)
                {
                    foreach (var item in newItems)
                    {
                        target.SelectedItems.Add(item);
                    }
                }

                SetIsUpdatingSelectedItems(target, false);
            });
        }
    }
}

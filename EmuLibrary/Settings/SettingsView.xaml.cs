using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EmuLibrary.Settings
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void Click_Delete(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is EmulatorMapping mapping)
            {
                var res = Settings.Instance.PlayniteAPI.Dialogs.ShowMessage(string.Format("Delete this mapping?\r\n\r\n{0}", mapping.GetDescriptionLines().Aggregate((a, b) => $"{a}{Environment.NewLine}{b}")), "Confirm delete", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.Yes)
                {
                    Settings.Instance.Mappings.Remove(mapping);
                }
            }
        }

        private void Click_AddSourcePath(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            string path;
            if ((path = GetSelectedFolderPath()) != null)
            {
                mapping.SourcePaths.Add(path);
            }
        }

        private void Click_ClearSourcePaths(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is EmulatorMapping mapping)
            {
                var res = Settings.Instance.PlayniteAPI.Dialogs.ShowMessage(
                    "Clear all source paths for this mapping?",
                    "Confirm clear",
                    MessageBoxButton.YesNo);

                if (res == MessageBoxResult.Yes)
                {
                    mapping.SourcePaths.Clear();
                }
            }
        }

        private void Click_BrowseDestination(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            string path;
            if ((path = GetSelectedFolderPath()) != null)
            {
                var playnite = Settings.Instance.PlayniteAPI;
                if (playnite.Paths.IsPortable)
                {
                    path = path.Replace(playnite.Paths.ApplicationPath, Playnite.SDK.ExpandableVariables.PlayniteDirectory);
                }

                mapping.DestinationPath = path;
            }
        }

        private static string GetSelectedFolderPath()
        {
            return Settings.Instance.PlayniteAPI.Dialogs.SelectFolder();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
            e.Handled = true;
        }
    }
}

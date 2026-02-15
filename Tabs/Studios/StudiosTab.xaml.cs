using System.Windows;
using System.Windows.Controls;
using classique.timetabler.Data;
using classique.timetabler.Models;

namespace classique.timetabler.Tabs.Studios
{
    public partial class StudiosTab : UserControl
    {
        public StudiosTab()
        {
            InitializeComponent();
        }

        private void AddStudio_Click(object sender, RoutedEventArgs e)
        {
            AppData.Current.Studios.Add(new Studio { Name = "New Studio" });
        }

        private void RemoveStudio_Click(object sender, RoutedEventArgs e)
        {
            if (StudiosDataGrid.SelectedItem is Studio studio)
            {
                AppData.Current.Studios.Remove(studio);
            }
        }
    }
}

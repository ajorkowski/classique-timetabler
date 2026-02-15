using System.Windows;
using System.Windows.Controls;
using classique.timetabler.Data;

namespace classique.timetabler.Tabs.Results
{
    public partial class ResultsTab : UserControl
    {
        public ResultsTab()
        {
            InitializeComponent();
            Loaded += ResultsTab_Loaded;
            IsVisibleChanged += ResultsTab_IsVisibleChanged;
        }

        private void ResultsTab_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshResults();
        }

        private void ResultsTab_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool isVisible && isVisible)
            {
                RefreshResults();
            }
        }

        public void RefreshResults()
        {
            var data = AppData.Current;
            var hasResults = data.ScheduledClasses.Count > 0;

            NoResultsPanel.Visibility = hasResults ? Visibility.Collapsed : Visibility.Visible;
            ScheduleResultsControl.Visibility = hasResults ? Visibility.Visible : Visibility.Collapsed;

            if (hasResults)
            {
                var groupCount = data.ScheduledClasses.Count(c => !c.IsSolo);
                var soloCount = data.ScheduledClasses.Count(c => c.IsSolo);
                ResultsInfoText.Text = $"{data.ScheduledClasses.Count} classes scheduled ({groupCount} groups, {soloCount} solos)";
                ScheduleResultsControl.SetScheduledClasses(data.ScheduledClasses);
            }
            else
            {
                ResultsInfoText.Text = "";
            }
        }
    }
}

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using classique.timetabler.Data;
using classique.timetabler.Models;

namespace classique.timetabler.Tabs.Teachers
{
    public partial class TeachersTab : UserControl
    {
        private Teacher? _selectedTeacher;
        private ObservableCollection<TeacherAvailability> _availability = new();

        public ObservableCollection<Studio> Studios => AppData.Current.Studios;

        public TeachersTab()
        {
            InitializeComponent();
            AvailabilityDataGrid.ItemsSource = _availability;
        }

        private void TeachersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedTeacher = TeachersListBox.SelectedItem as Teacher;

            if (_selectedTeacher != null)
            {
                TeacherNameTextBox.Text = _selectedTeacher.Name;
                _availability.Clear();
                foreach (var slot in _selectedTeacher.Availability)
                {
                    _availability.Add(slot);
                }
                UpdateWorkloadPanel();
                UpdateWarningPanel();
            }
            else
            {
                TeacherNameTextBox.Text = string.Empty;
                _availability.Clear();
                WorkloadPanel.Visibility = Visibility.Collapsed;
                WarningPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void TeacherNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedTeacher != null)
            {
                _selectedTeacher.Name = TeacherNameTextBox.Text;
                RefreshListBox();
            }
        }

        private void AddTeacher_Click(object sender, RoutedEventArgs e)
        {
            var teacher = new Teacher { Name = "New Teacher" };
            AppData.Current.Teachers.Add(teacher);
            TeachersListBox.SelectedItem = teacher;
        }

        private void RemoveTeacher_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTeacher != null)
            {
                AppData.Current.Teachers.Remove(_selectedTeacher);
                _selectedTeacher = null;
                TeacherNameTextBox.Text = string.Empty;
                _availability.Clear();
                WorkloadPanel.Visibility = Visibility.Collapsed;
                WarningPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void AddAvailability_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTeacher != null)
            {
                var firstStudio = AppData.Current.Studios.FirstOrDefault();
                var slot = new TeacherAvailability
                {
                    StudioId = firstStudio?.Id ?? Guid.Empty,
                    Day = DayOfWeek.Monday,
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(17, 0)
                };
                _selectedTeacher.Availability.Add(slot);
                _availability.Add(slot);
                UpdateValidation();
            }
        }

        private void RemoveAvailability_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTeacher != null && AvailabilityDataGrid.SelectedItem is TeacherAvailability slot)
            {
                _selectedTeacher.Availability.Remove(slot);
                _availability.Remove(slot);
                UpdateValidation();
            }
        }

        private void AvailabilityDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                // Defer validation update until after the edit is complete
                Dispatcher.BeginInvoke(new Action(() => UpdateValidation()), 
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void UpdateValidation()
        {
            if (_selectedTeacher != null)
            {
                _selectedTeacher.NotifyValidationChanged();
                UpdateWorkloadPanel();
                UpdateWarningPanel();
                RefreshListBox();
            }
        }

        private void UpdateWorkloadPanel()
        {
            if (_selectedTeacher == null)
            {
                WorkloadPanel.Visibility = Visibility.Collapsed;
                return;
            }

            WorkloadPanel.Visibility = Visibility.Visible;
            
            GroupsCountText.Text = $"{_selectedTeacher.GroupCount} ({FormatDuration(_selectedTeacher.GroupDuration)})";
            SolosCountText.Text = $"{_selectedTeacher.SoloCount} ({FormatDuration(_selectedTeacher.SoloDuration)})";
            TotalWorkloadText.Text = $"{FormatDuration(_selectedTeacher.TotalWorkload)} / {FormatDuration(_selectedTeacher.TotalAvailability)}";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            }
            return $"{(int)duration.TotalMinutes}m";
        }

        private void UpdateWarningPanel()
        {
            if (_selectedTeacher != null && _selectedTeacher.HasWarnings)
            {
                WarningPanel.Visibility = Visibility.Visible;
                WarningText.Text = _selectedTeacher.WarningsSummary;
            }
            else
            {
                WarningPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshListBox()
        {
            var index = TeachersListBox.SelectedIndex;
            TeachersListBox.Items.Refresh();
            TeachersListBox.SelectedIndex = index;
        }
    }
}

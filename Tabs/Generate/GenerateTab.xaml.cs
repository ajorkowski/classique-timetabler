using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using classique.timetabler.Data;
using classique.timetabler.Dialogs;

namespace classique.timetabler.Tabs.Generate
{
    public partial class GenerateTab : UserControl
    {
        private ObservableCollection<string> _warnings = new();
        private bool _isUpdating;

        public GenerateTab()
        {
            InitializeComponent();
            WarningsList.ItemsSource = _warnings;
            Loaded += GenerateTab_Loaded;
            IsVisibleChanged += GenerateTab_IsVisibleChanged;
        }

        private void GenerateTab_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshSummary();
            LoadWeights();
        }

        private void GenerateTab_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool isVisible && isVisible)
            {
                RefreshSummary();
                LoadWeights();
            }
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SolverProgressDialog
            {
                Owner = Window.GetWindow(this)
            };

            var dialogResult = dialog.ShowDialog();

            if (dialogResult == true && dialog.Result != null)
            {
                // Notify the MainWindow to update the Results tab
                if (Window.GetWindow(this) is MainWindow mainWindow)
                {
                    mainWindow.OnScheduleAccepted();
                }

                MessageBox.Show(
                    $"Timetable generated successfully!\n\n" +
                    $"Scheduled {dialog.Result.ScheduledClasses.Count} classes.\n" +
                    $"Solve time: {dialog.Result.SolveTime.TotalSeconds:F2}s\n\n" +
                    $"Go to the Results tab to view the schedule.",
                    "Generation Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void LoadWeights()
        {
            _isUpdating = true;
            var data = AppData.Current;
            AlphaTextBox.Text = data.AlphaMakespan.ToString();
            BetaTextBox.Text = data.BetaStudentClustering.ToString();
            GammaTextBox.Text = data.GammaAgePriority.ToString();
            CrossDayTextBox.Text = data.CrossDayPenalty.ToString();
            _isUpdating = false;
        }

        private void WeightTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            var data = AppData.Current;

            if (sender == AlphaTextBox && long.TryParse(AlphaTextBox.Text, out long alpha))
            {
                data.AlphaMakespan = alpha;
            }
            else if (sender == BetaTextBox && long.TryParse(BetaTextBox.Text, out long beta))
            {
                data.BetaStudentClustering = beta;
            }
            else if (sender == GammaTextBox && long.TryParse(GammaTextBox.Text, out long gamma))
            {
                data.GammaAgePriority = gamma;
            }
            else if (sender == CrossDayTextBox && long.TryParse(CrossDayTextBox.Text, out long crossDay))
            {
                data.CrossDayPenalty = crossDay;
            }
        }

        private void RefreshSummary()
        {
            var data = AppData.Current;

            // Update counts
            StudiosCountText.Text = data.Studios.Count.ToString();
            TeachersCountText.Text = data.Teachers.Count.ToString();
            StudentsCountText.Text = data.Students.Count.ToString();
            GroupsCountText.Text = data.Groups.Count.ToString();

            // Count total solos
            int totalSolos = 0;
            TimeSpan totalSoloDuration = TimeSpan.Zero;
            foreach (var student in data.Students)
            {
                totalSolos += student.Solos.Count;
                foreach (var solo in student.Solos)
                {
                    totalSoloDuration += TimeSpan.FromMinutes(solo.DurationMinutes);
                }
            }
            SolosCountText.Text = totalSolos.ToString();

            // Calculate total duration (groups + solos)
            TimeSpan totalGroupDuration = TimeSpan.Zero;
            foreach (var group in data.Groups)
            {
                totalGroupDuration += group.Duration;
            }
            var totalDuration = totalGroupDuration + totalSoloDuration;
            TotalDurationText.Text = FormatDuration(totalDuration);

            // Collect all warnings
            _warnings.Clear();
            
            // Check for missing data
            if (data.Studios.Count == 0)
            {
                _warnings.Add("No studios defined");
            }

            if (data.Teachers.Count == 0)
            {
                _warnings.Add("No teachers defined");
            }

            if (data.Groups.Count == 0 && totalSolos == 0)
            {
                _warnings.Add("No groups or solos to schedule");
            }

            // Teacher warnings
            foreach (var teacher in data.Teachers)
            {
                teacher.NotifyValidationChanged();
                if (teacher.HasWarnings)
                {
                    foreach (var warning in teacher.ValidationWarnings)
                    {
                        _warnings.Add($"Teacher '{teacher.Name}': {warning}");
                    }
                }
            }

            // Group warnings
            foreach (var group in data.Groups)
            {
                // Include group validation warnings (teacher, studio, flexible constraints)
                if (group.HasWarnings)
                {
                    foreach (var warning in group.ValidationWarnings)
                    {
                        _warnings.Add($"Group '{group.Name}': {warning}");
                    }
                }
                if (group.StudentCount == 0)
                {
                    _warnings.Add($"Group '{group.Name}': No students enrolled");
                }
            }

            // Student solo warnings
            foreach (var student in data.Students)
            {
                foreach (var solo in student.Solos)
                {
                    if (!solo.TeacherId.HasValue)
                    {
                        _warnings.Add($"Student '{student.Name}' solo '{solo.Name}': No teacher assigned");
                    }
                }
            }

            // Update UI based on warnings
            bool hasWarnings = _warnings.Count > 0;
            NoWarningsPanel.Visibility = hasWarnings ? Visibility.Collapsed : Visibility.Visible;
            WarningsPanel.Visibility = hasWarnings ? Visibility.Visible : Visibility.Collapsed;
            WarningsCountText.Text = $"{_warnings.Count} warning(s) found:";
            
            GenerateButton.IsEnabled = !hasWarnings;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            }
            return $"{(int)duration.TotalMinutes}m";
        }
    }
}

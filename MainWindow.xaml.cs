using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using classique.timetabler.Data;

namespace classique.timetabler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string? _currentFilePath;
        private DispatcherTimer? _autoSaveHideTimer;

        public MainWindow()
        {
            InitializeComponent();
            UpdateContinueButtonState();
            
            // Subscribe to autosave events
            AutoSaveService.AutoSaveStarted += OnAutoSaveStarted;
            AutoSaveService.AutoSaveCompleted += OnAutoSaveCompleted;
        }

        private void OnAutoSaveStarted(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _autoSaveHideTimer?.Stop();
                AutoSaveIndicator.Visibility = Visibility.Visible;
            });
        }

        private void OnAutoSaveCompleted(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Hide the indicator after a short delay so user can see it
                _autoSaveHideTimer?.Stop();
                _autoSaveHideTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                _autoSaveHideTimer.Tick += (s, args) =>
                {
                    AutoSaveIndicator.Visibility = Visibility.Collapsed;
                    _autoSaveHideTimer.Stop();
                };
                _autoSaveHideTimer.Start();
            });
        }

        private void UpdateContinueButtonState()
        {
            ContinueButton.IsEnabled = AutoSaveService.HasAutoSave();
        }

        private void StartNewButton_Click(object sender, RoutedEventArgs e)
        {
            AppData.Reset();
            AutoSaveService.ClearAutoSave();
            _currentFilePath = null;
            StartApplication();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (AutoSaveService.TryRecover())
            {
                _currentFilePath = null;
                StartApplication();
            }
            else
            {
                MessageBox.Show("Failed to recover previous session.", "Recovery Failed", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            // Open file dialog to load a saved timetable
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Timetable Files (*.timetable)|*.timetable|All Files (*.*)|*.*",
                Title = "Load Timetable"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                if (FileService.TryLoad(openFileDialog.FileName))
                {
                    _currentFilePath = openFileDialog.FileName;
                    StartApplication();
                }
                else
                {
                    MessageBox.Show("Failed to load the timetable file.", "Load Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                SaveAsButton_Click(sender, e);
            }
            else
            {
                try
                {
                    FileService.Save(_currentFilePath);
                }
                catch
                {
                    MessageBox.Show("Failed to save the timetable file.", "Save Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Timetable Files (*.timetable)|*.timetable|All Files (*.*)|*.*",
                Title = "Save Timetable",
                DefaultExt = ".timetable"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    FileService.Save(saveFileDialog.FileName);
                    _currentFilePath = saveFileDialog.FileName;
                }
                catch
                {
                    MessageBox.Show("Failed to save the timetable file.", "Save Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BackToStartButton_Click(object sender, RoutedEventArgs e)
        {
            AutoSaveService.StopWatching();
            AutoSaveService.Save();
            
            MainContent.Visibility = Visibility.Collapsed;
            IntroScreen.Visibility = Visibility.Visible;
            UpdateContinueButtonState();
        }

        private void StartApplication()
        {
            RefreshTabDataContexts();
            UpdateResultsTabState();
            
            // Hide intro screen and show main content
            IntroScreen.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
            AutoSaveService.StartWatching();
        }

        /// <summary>
        /// Called by GenerateTab when a schedule is accepted.
        /// </summary>
        public void OnScheduleAccepted()
        {
            UpdateResultsTabState();
        }

        private void UpdateResultsTabState()
        {
            var hasResults = AppData.Current.ScheduledClasses.Count > 0;
            ResultsTabItem.IsEnabled = hasResults;
            
            if (hasResults)
            {
                ResultsTab.RefreshResults();
            }
        }

        private void RefreshTabDataContexts()
        {
            TeachersTab.DataContext = AppData.Current;
            StudiosTab.DataContext = AppData.Current;
            GroupsTab.DataContext = AppData.Current;
            StudentsTab.DataContext = AppData.Current;
            GenerateTab.DataContext = AppData.Current;
            ResultsTab.DataContext = AppData.Current;
        }

        protected override void OnClosed(EventArgs e)
        {
            AutoSaveService.AutoSaveStarted -= OnAutoSaveStarted;
            AutoSaveService.AutoSaveCompleted -= OnAutoSaveCompleted;
            AutoSaveService.Save();
            AutoSaveService.StopWatching();
            base.OnClosed(e);
        }
    }
}
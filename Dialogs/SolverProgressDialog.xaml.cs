using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using classique.timetabler.Data;
using classique.timetabler.Models;
using classique.timetabler.Solver;

namespace classique.timetabler.Dialogs
{
    public partial class SolverProgressDialog : Window
    {
        private readonly BackgroundWorker _worker;
        private readonly DispatcherTimer _elapsedTimer;
        private readonly DateTime _startTime;
        private CancellationTokenSource? _cancellationSource;
        private ScheduleResult? _result;

        public ScheduleResult? Result => _result;

        public SolverProgressDialog()
        {
            InitializeComponent();
            
            _startTime = DateTime.Now;
            _worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            _worker.DoWork += Worker_DoWork;
            _worker.ProgressChanged += Worker_ProgressChanged;
            _worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

            _elapsedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _elapsedTimer.Tick += ElapsedTimer_Tick;

            Loaded += SolverProgressDialog_Loaded;
        }

        private void SolverProgressDialog_Loaded(object sender, RoutedEventArgs e)
        {
            _cancellationSource = new CancellationTokenSource();
            _elapsedTimer.Start();
            _worker.RunWorkerAsync();
        }

        private void ElapsedTimer_Tick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _startTime;
            ElapsedTimeText.Text = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds / 100}";
        }

        private void Worker_DoWork(object? sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            var progress = new SolverProgress(worker!);

            try
            {
                progress.Log("Starting timetable generation...");
                progress.Log("");

                // Create solver with progress reporting
                var data = TimetableData.FromAppData(AppData.Current);
                var solver = new TimetableSolverWithProgress(data, 120, progress);

                // Run the solver
                _result = solver.Solve();
            }
            catch (Exception ex)
            {
                progress.Log($"ERROR: {ex.Message}");
                _result = new ScheduleResult
                {
                    IsFeasible = false,
                    SolverMessage = $"Solver error: {ex.Message}"
                };
            }
        }

        private void Worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            if (e.UserState is SolverProgressUpdate update)
            {
                switch (update.Type)
                {
                    case ProgressUpdateType.Log:
                        AppendLog(update.Message);
                        break;
                    case ProgressUpdateType.Status:
                        StatusText.Text = update.Message;
                        break;
                    case ProgressUpdateType.ClassCount:
                        ClassCountText.Text = update.Message;
                        break;
                    case ProgressUpdateType.AlternativesCount:
                        AlternativesCountText.Text = update.Message;
                        break;
                    case ProgressUpdateType.VariablesCount:
                        VariablesCountText.Text = update.Message;
                        break;
                }
            }
        }

        private void AppendLog(string message)
        {
            LogTextBox.Text += message + Environment.NewLine;
            LogScrollViewer.ScrollToEnd();
        }

        private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            _elapsedTimer.Stop();

            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 100;

            if (_result != null)
            {
                if (_result.IsFeasible)
                {
                    StatusText.Text = _result.IsOptimal ? "\u2713 Optimal solution found!" : "\u2713 Feasible solution found";
                    AppendLog("");
                    AppendLog("=".PadRight(50, '='));
                    AppendLog(_result.IsOptimal ? "OPTIMAL SOLUTION FOUND" : "FEASIBLE SOLUTION FOUND");
                    AppendLog($"Scheduled {_result.ScheduledClasses.Count} classes");
                    AppendLog($"Objective value: {_result.ObjectiveValue:N0}");
                    AppendLog($"Total solve time: {_result.SolveTime.TotalSeconds:F2}s");
                    
                    // Show View Results button
                    ViewResultsButton.Visibility = Visibility.Visible;
                }
                else
                {
                    StatusText.Text = "\u2717 No solution found";
                    AppendLog("");
                    AppendLog("=".PadRight(50, '='));
                    AppendLog("NO SOLUTION FOUND");
                    AppendLog(_result.SolverMessage ?? "Unknown error");
                }
            }

            CancelButton.Visibility = Visibility.Collapsed;
            CloseButton.Visibility = Visibility.Visible;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationSource?.Cancel();
            _worker.CancelAsync();
            StatusText.Text = "Cancelling...";
            CancelButton.IsEnabled = false;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ViewResultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_result == null || !_result.IsFeasible) return;

            // Update results header
            ResultsHeaderText.Text = _result.IsOptimal ? "Optimal Schedule Found" : "Feasible Schedule Found";
            ResultsSubText.Text = $"{_result.ScheduledClasses.Count} classes scheduled in {_result.SolveTime.TotalSeconds:F1}s | Objective: {_result.ObjectiveValue:N0}";

            // Populate the results view
            ScheduleResultsControl.SetScheduledClasses(_result.ScheduledClasses);

            // Switch views
            ProgressView.Visibility = Visibility.Collapsed;
            ResultsView.Visibility = Visibility.Visible;
            
            Title = "Schedule Results";
        }

        private void BackToLogButton_Click(object sender, RoutedEventArgs e)
        {
            // Switch back to progress view
            ResultsView.Visibility = Visibility.Collapsed;
            ProgressView.Visibility = Visibility.Visible;
            
            Title = "Generating Timetable";
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (_result != null && _result.IsFeasible)
            {
                // Save the scheduled classes to AppData
                AppData.Current.LastScheduleResult = _result;
                AppData.Current.ScheduledClasses = _result.ScheduledClasses;
            }
            
            DialogResult = true;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_worker.IsBusy)
            {
                _cancellationSource?.Cancel();
                _worker.CancelAsync();
            }
            _elapsedTimer.Stop();
            base.OnClosing(e);
        }
    }

    public enum ProgressUpdateType
    {
        Log,
        Status,
        ClassCount,
        AlternativesCount,
        VariablesCount
    }

    public class SolverProgressUpdate
    {
        public ProgressUpdateType Type { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SolverProgress
    {
        private readonly BackgroundWorker _worker;

        public SolverProgress(BackgroundWorker worker)
        {
            _worker = worker;
        }

        public void Log(string message)
        {
            _worker.ReportProgress(0, new SolverProgressUpdate { Type = ProgressUpdateType.Log, Message = message });
        }

        public void SetStatus(string status)
        {
            _worker.ReportProgress(0, new SolverProgressUpdate { Type = ProgressUpdateType.Status, Message = status });
        }

        public void SetClassCount(int count)
        {
            _worker.ReportProgress(0, new SolverProgressUpdate { Type = ProgressUpdateType.ClassCount, Message = count.ToString() });
        }

        public void SetAlternativesCount(int count)
        {
            _worker.ReportProgress(0, new SolverProgressUpdate { Type = ProgressUpdateType.AlternativesCount, Message = count.ToString("N0") });
        }

        public void SetVariablesCount(int count)
        {
            _worker.ReportProgress(0, new SolverProgressUpdate { Type = ProgressUpdateType.VariablesCount, Message = count.ToString("N0") });
        }
    }
}

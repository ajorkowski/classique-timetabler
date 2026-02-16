using System.Collections.ObjectModel;
using ClassiqueTimetabler.Maui.Data;
using ClassiqueTimetabler.Maui.Models;
using ClassiqueTimetabler.Maui.Solver;

namespace ClassiqueTimetabler.Maui.Views;

public partial class GenerateTab : ContentView
{
    private ObservableCollection<string> _warnings = new();
    private bool _isUpdating;
    private bool _isGenerating;
    private CancellationTokenSource? _cancellationSource;
    private DateTime _startTime;
    private IDispatcherTimer? _elapsedTimer;
    private ScheduleResult? _currentResult;

    public event EventHandler<ScheduleResult>? ScheduleGenerated;

    public GenerateTab()
    {
        InitializeComponent();
        Loaded += GenerateTab_Loaded;
    }

    private void GenerateTab_Loaded(object? sender, EventArgs e)
    {
        RefreshSummary();
        LoadWeights();
    }

    public void Refresh()
    {
        RefreshSummary();
        LoadWeights();
    }

    private void ShowSetupView()
    {
        SetupView.IsVisible = true;
        ProgressView.IsVisible = false;
        ResultsView.IsVisible = false;
    }

    private void ShowProgressView()
    {
        SetupView.IsVisible = false;
        ProgressView.IsVisible = true;
        ResultsView.IsVisible = false;
    }

    private void ShowResultsView()
    {
        SetupView.IsVisible = false;
        ProgressView.IsVisible = false;
        ResultsView.IsVisible = true;
    }

    private void LoadWeights()
    {
        _isUpdating = true;
        var data = AppData.Current;
        AlphaEntry.Text = data.AlphaMakespan.ToString();
        BetaEntry.Text = data.BetaStudentClustering.ToString();
        GammaEntry.Text = data.GammaAgePriority.ToString();
        CrossDayEntry.Text = data.CrossDayPenalty.ToString();
        _isUpdating = false;
    }

    private void WeightEntry_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        var data = AppData.Current;

        if (sender == AlphaEntry && long.TryParse(AlphaEntry.Text, out long alpha))
        {
            data.AlphaMakespan = alpha;
        }
        else if (sender == BetaEntry && long.TryParse(BetaEntry.Text, out long beta))
        {
            data.BetaStudentClustering = beta;
        }
        else if (sender == GammaEntry && long.TryParse(GammaEntry.Text, out long gamma))
        {
            data.GammaAgePriority = gamma;
        }
        else if (sender == CrossDayEntry && long.TryParse(CrossDayEntry.Text, out long crossDay))
        {
            data.CrossDayPenalty = crossDay;
        }
    }

    private void RefreshSummary()
    {
        var data = AppData.Current;

        // Update counts
        StudiosCountLabel.Text = data.Studios.Count.ToString();
        TeachersCountLabel.Text = data.Teachers.Count.ToString();
        StudentsCountLabel.Text = data.Students.Count.ToString();
        GroupsCountLabel.Text = data.Groups.Count.ToString();

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
        SolosCountLabel.Text = totalSolos.ToString();

        // Calculate total duration (groups + solos)
        TimeSpan totalGroupDuration = TimeSpan.Zero;
        foreach (var group in data.Groups)
        {
            totalGroupDuration += group.Duration;
        }
        var totalDuration = totalGroupDuration + totalSoloDuration;
        TotalDurationLabel.Text = FormatDuration(totalDuration);

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
        NoWarningsPanel.IsVisible = !hasWarnings;
        WarningsPanel.IsVisible = hasWarnings;
        WarningsCountLabel.Text = $"{_warnings.Count} warning(s) found:";

        WarningsList.Children.Clear();
        foreach (var warning in _warnings)
        {
            WarningsList.Children.Add(new Label
            {
                Text = $"• {warning}",
                TextColor = Color.FromArgb("#856404"),
                FontSize = 12
            });
        }

        GenerateButton.IsEnabled = !hasWarnings && !_isGenerating;
    }

    private async void GenerateButton_Clicked(object? sender, EventArgs e)
    {
        if (_isGenerating) return;

        _isGenerating = true;
        _cancellationSource = new CancellationTokenSource();
        _startTime = DateTime.Now;
        _currentResult = null;

        // Switch to progress view
        ShowProgressView();

        // Reset progress UI
        LogEditor.Text = "";
        StatusLabel.Text = "Initializing solver...";
        ClassCountLabel.Text = "-";
        AlternativesCountLabel.Text = "-";
        VariablesCountLabel.Text = "-";
        ElapsedTimeLabel.Text = "00:00.0";
        CancelButton.IsVisible = true;
        CancelButton.IsEnabled = true;
        ViewResultsButton.IsVisible = false;
        CloseProgressButton.IsVisible = false;

        // Start elapsed timer
        _elapsedTimer = Dispatcher.CreateTimer();
        _elapsedTimer.Interval = TimeSpan.FromMilliseconds(100);
        _elapsedTimer.Tick += (s, e) =>
        {
            var elapsed = DateTime.Now - _startTime;
            ElapsedTimeLabel.Text = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds / 100}";
        };
        _elapsedTimer.Start();

        try
        {
            var progress = new Progress<SolverProgressUpdate>(update =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    switch (update.Type)
                    {
                        case ProgressUpdateType.Log:
                            LogEditor.Text += update.Message + Environment.NewLine;
                            break;
                        case ProgressUpdateType.Status:
                            StatusLabel.Text = update.Message;
                            break;
                        case ProgressUpdateType.ClassCount:
                            ClassCountLabel.Text = update.Message;
                            break;
                        case ProgressUpdateType.AlternativesCount:
                            AlternativesCountLabel.Text = update.Message;
                            break;
                        case ProgressUpdateType.VariablesCount:
                            VariablesCountLabel.Text = update.Message;
                            break;
                    }
                });
            });

            var result = await Task.Run(() =>
            {
                var data = TimetableData.FromAppData(AppData.Current);
                var solver = new TimetableSolverWithProgress(data, 120, new SolverProgress(progress));
                return solver.Solve();
            }, _cancellationSource.Token);

            _elapsedTimer.Stop();
            _currentResult = result;

            if (result.IsFeasible)
            {
                StatusLabel.Text = result.IsOptimal ? "✓ Optimal solution found!" : "✓ Feasible solution found";
                LogEditor.Text += Environment.NewLine;
                LogEditor.Text += new string('=', 50) + Environment.NewLine;
                LogEditor.Text += (result.IsOptimal ? "OPTIMAL SOLUTION FOUND" : "FEASIBLE SOLUTION FOUND") + Environment.NewLine;
                LogEditor.Text += $"Scheduled {result.ScheduledClasses.Count} classes" + Environment.NewLine;
                LogEditor.Text += $"Objective value: {result.ObjectiveValue:N0}" + Environment.NewLine;
                LogEditor.Text += $"Total solve time: {result.SolveTime.TotalSeconds:F2}s" + Environment.NewLine;

                // Show View Results button
                ViewResultsButton.IsVisible = true;
            }
            else
            {
                StatusLabel.Text = "✗ No solution found";
                LogEditor.Text += Environment.NewLine;
                LogEditor.Text += new string('=', 50) + Environment.NewLine;
                LogEditor.Text += "NO SOLUTION FOUND" + Environment.NewLine;
                LogEditor.Text += result.SolverMessage ?? "Unknown error" + Environment.NewLine;
            }
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = "Cancelled";
            LogEditor.Text += Environment.NewLine + "Operation cancelled by user." + Environment.NewLine;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Error";
            LogEditor.Text += Environment.NewLine + $"ERROR: {ex.Message}" + Environment.NewLine;
        }
        finally
        {
            _elapsedTimer?.Stop();
            _isGenerating = false;
            CancelButton.IsVisible = false;
            CloseProgressButton.IsVisible = true;
        }
    }

    private void CancelButton_Clicked(object? sender, EventArgs e)
    {
        _cancellationSource?.Cancel();
        CancelButton.IsEnabled = false;
        StatusLabel.Text = "Cancelling...";
    }

    private void ViewResultsButton_Clicked(object? sender, EventArgs e)
    {
        if (_currentResult == null || !_currentResult.IsFeasible) return;

        // Update results header
        ResultsHeaderLabel.Text = _currentResult.IsOptimal ? "Optimal Schedule Found" : "Feasible Schedule Found";
        ResultsSubLabel.Text = $"{_currentResult.ScheduledClasses.Count} classes scheduled in {_currentResult.SolveTime.TotalSeconds:F1}s | Objective: {_currentResult.ObjectiveValue:N0}";

        // Populate the results preview
        ResultsPreview.SetScheduledClasses(_currentResult.ScheduledClasses);

        // Switch to results view
        ShowResultsView();
    }

    private void CloseProgressButton_Clicked(object? sender, EventArgs e)
    {
        ShowSetupView();
        RefreshSummary();
    }

    private void ViewLogButton_Clicked(object? sender, EventArgs e)
    {
        // Go back to progress view to see the log
        ShowProgressView();
    }

    private void RegenerateButton_Clicked(object? sender, EventArgs e)
    {
        // Go back to setup view and refresh
        ShowSetupView();
        RefreshSummary();
    }

    private async void SaveToResultsButton_Clicked(object? sender, EventArgs e)
    {
        if (_currentResult != null && _currentResult.IsFeasible)
        {
            // Save the scheduled classes to AppData
            AppData.Current.LastScheduleResult = _currentResult;
            AppData.Current.ScheduledClasses = _currentResult.ScheduledClasses;

            // Notify that schedule was accepted
            ScheduleGenerated?.Invoke(this, _currentResult);

            // Go back to setup view
            ShowSetupView();
            RefreshSummary();

            // Show confirmation
            if (Window?.Page is Page page)
            {
                await page.DisplayAlertAsync(
                    "Saved",
                    "Schedule has been saved to the Results tab.",
                    "OK");
            }
        }
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

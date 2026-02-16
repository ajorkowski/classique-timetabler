using ClassiqueTimetabler.Maui.Data;

namespace ClassiqueTimetabler.Maui.Views;

public partial class ResultsTab : ContentView
{
    public ResultsTab()
    {
        InitializeComponent();
        Loaded += ResultsTab_Loaded;
    }

    private void ResultsTab_Loaded(object? sender, EventArgs e)
    {
        RefreshResults();
    }

    public void RefreshResults()
    {
        var data = AppData.Current;
        var hasResults = data.ScheduledClasses.Count > 0;

        NoResultsPanel.IsVisible = !hasResults;
        ScheduleResultsControl.IsVisible = hasResults;

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

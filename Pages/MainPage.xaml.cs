using ClassiqueTimetabler.Maui.Data;
using ClassiqueTimetabler.Maui.Views;
using CommunityToolkit.Maui.Storage;

namespace ClassiqueTimetabler.Maui.Pages;

public partial class MainPage : ContentPage
{
    private Button? _selectedTabButton;
    private string? _currentFilePath;
    private TeachersTab? _teachersTabView;
    private GroupsTab? _groupsTabView;
    private StudentsTab? _studentsTabView;
    private GenerateTab? _generateTabView;
    private ResultsTab? _resultsTabView;

    private TeachersTab TeachersTabView => _teachersTabView ??= new TeachersTab();
    private GroupsTab GroupsTabView => _groupsTabView ??= new GroupsTab();
    private StudentsTab StudentsTabView => _studentsTabView ??= new StudentsTab();
    private GenerateTab GenerateTabView
    {
        get
        {
            if (_generateTabView == null)
            {
                _generateTabView = new GenerateTab();
                _generateTabView.ScheduleGenerated += (s, result) => ResultsTabView.RefreshResults();
            }
            return _generateTabView;
        }
    }
    private ResultsTab ResultsTabView => _resultsTabView ??= new ResultsTab();

    public MainPage()
    {
        InitializeComponent();
        _selectedTabButton = StudiosTabButton;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AutoSaveService.StartWatching();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        AutoSaveService.StopWatching();
    }

    private void TabButton_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button clickedButton) return;

        // Reset previous tab button to unselected style
        if (_selectedTabButton != null)
        {
            _selectedTabButton.BackgroundColor = Color.FromArgb("#F0F0F0");
            _selectedTabButton.TextColor = Color.FromArgb("#212121");
        }

        // Highlight selected tab with primary color
        clickedButton.BackgroundColor = Color.FromArgb("#2563EB");
        clickedButton.TextColor = Colors.White;
        _selectedTabButton = clickedButton;

        // Switch tab content
        TabContent.Content = clickedButton.Text switch
        {
            "Studios" => StudiosTabView,
            "Teachers" => TeachersTabView,
            "Groups" => GroupsTabView,
            "Students" => StudentsTabView,
            "Generate" => GenerateTabView,
            "Results" => ResultsTabView,
            _ => StudiosTabView
        };
    }

    private async void SaveButton_Clicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveAsButton_Clicked(sender, e);
        }
        else
        {
            try
            {
                FileService.Save(_currentFilePath);
            }
            catch
            {
                await DisplayAlertAsync("Save Failed", "Failed to save the timetable file.", "OK");
            }
        }
    }

    private async void SaveAsButton_Clicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FileSaver.Default.SaveAsync("timetable.timetable", new MemoryStream(), CancellationToken.None);
            if (result.IsSuccessful && !string.IsNullOrEmpty(result.FilePath))
            {
                FileService.Save(result.FilePath);
                _currentFilePath = result.FilePath;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Save Failed", $"Failed to save: {ex.Message}", "OK");
        }
    }

    private async void BackToStartButton_Clicked(object? sender, EventArgs e)
    {
        AutoSaveService.StopWatching();
        AutoSaveService.Save();
        await Shell.Current.GoToAsync("//Home");
    }
}

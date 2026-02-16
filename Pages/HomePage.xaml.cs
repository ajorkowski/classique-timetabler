using ClassiqueTimetabler.Maui.Data;

namespace ClassiqueTimetabler.Maui.Pages;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
        UpdateContinueButtonState();
    }

    private void UpdateContinueButtonState()
    {
        ContinueButton.IsEnabled = AutoSaveService.HasAutoSave();
    }

    private async void StartNewButton_Clicked(object? sender, EventArgs e)
    {
        AppData.Reset();
        AutoSaveService.ClearAutoSave();
        await Shell.Current.GoToAsync("//Main");
    }

    private async void ContinueButton_Clicked(object? sender, EventArgs e)
    {
        if (AutoSaveService.TryRecover())
        {
            await Shell.Current.GoToAsync("//Main");
        }
        else
        {
            await DisplayAlertAsync("Recovery Failed", "Failed to recover previous session.", "OK");
        }
    }

    private async void LoadButton_Clicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Load Timetable",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".timetable" } }
                })
            });

            if (result != null)
            {
                if (FileService.TryLoad(result.FullPath))
                {
                    await Shell.Current.GoToAsync("//Main");
                }
                else
                {
                    await DisplayAlertAsync("Load Failed", "Failed to load the timetable file.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to load file: {ex.Message}", "OK");
        }
    }
}

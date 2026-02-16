using ClassiqueTimetabler.Maui.Data;
using ClassiqueTimetabler.Maui.Models;

namespace ClassiqueTimetabler.Maui.Views;

public partial class StudiosTab : ContentView
{
    public StudiosTab()
    {
        InitializeComponent();
        BindingContext = AppData.Current;
    }

    private void AddStudio_Clicked(object? sender, EventArgs e)
    {
        AppData.Current.Studios.Add(new Studio { Name = "New Studio" });
    }

    private void RemoveStudio_Clicked(object? sender, EventArgs e)
    {
        if (StudiosCollectionView.SelectedItem is Studio studio)
        {
            AppData.Current.Studios.Remove(studio);
        }
    }
}

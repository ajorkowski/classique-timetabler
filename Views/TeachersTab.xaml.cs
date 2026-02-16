using System.Collections.ObjectModel;
using ClassiqueTimetabler.Maui.Data;
using ClassiqueTimetabler.Maui.Models;

namespace ClassiqueTimetabler.Maui.Views;

public partial class TeachersTab : ContentView
{
    private Teacher? _selectedTeacher;
    private TeacherAvailability? _selectedAvailability;
    private readonly ObservableCollection<TeacherAvailability> _availability = new();
    private bool _isUpdatingEditPanel;

    public TeachersTab()
    {
        InitializeComponent();
        TeachersCollectionView.ItemsSource = AppData.Current.Teachers;
        AvailabilityCollectionView.ItemsSource = _availability;
        PopulateStudioPicker();
    }

    private void PopulateStudioPicker()
    {
        StudioPicker.ItemsSource = AppData.Current.Studios.Select(s => s.Name).ToList();
    }

    private void TeachersCollectionView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedTeacher = TeachersCollectionView.SelectedItem as Teacher;
        _selectedAvailability = null;
        EditPanel.IsVisible = false;

        if (_selectedTeacher != null)
        {
            TeacherNameEntry.Text = _selectedTeacher.Name;
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
            TeacherNameEntry.Text = string.Empty;
            _availability.Clear();
            WorkloadPanel.IsVisible = false;
            WarningPanel.IsVisible = false;
        }
    }

    private void AvailabilityCollectionView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedAvailability = AvailabilityCollectionView.SelectedItem as TeacherAvailability;
        
        if (_selectedAvailability != null)
        {
            _isUpdatingEditPanel = true;
            EditPanel.IsVisible = true;
            
            // Update studio picker
            PopulateStudioPicker();
            var studioIndex = AppData.Current.Studios.ToList().FindIndex(s => s.Id == _selectedAvailability.StudioId);
            StudioPicker.SelectedIndex = studioIndex >= 0 ? studioIndex : 0;
            
            // Update day picker
            DayPicker.SelectedIndex = (int)_selectedAvailability.Day;
            
            // Update time pickers
            StartTimePicker.Time = _selectedAvailability.StartTime.ToTimeSpan();
            EndTimePicker.Time = _selectedAvailability.EndTime.ToTimeSpan();
            
            _isUpdatingEditPanel = false;
        }
        else
        {
            EditPanel.IsVisible = false;
        }
    }

    private void TeacherNameEntry_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_selectedTeacher != null)
        {
            _selectedTeacher.Name = TeacherNameEntry.Text ?? string.Empty;
            RefreshTeachersList();
        }
    }

    private void AddTeacher_Clicked(object? sender, EventArgs e)
    {
        var teacher = new Teacher { Name = "New Teacher" };
        AppData.Current.Teachers.Add(teacher);
        TeachersCollectionView.SelectedItem = teacher;
    }

    private void RemoveTeacher_Clicked(object? sender, EventArgs e)
    {
        if (_selectedTeacher != null)
        {
            AppData.Current.Teachers.Remove(_selectedTeacher);
            _selectedTeacher = null;
            TeacherNameEntry.Text = string.Empty;
            _availability.Clear();
            WorkloadPanel.IsVisible = false;
            WarningPanel.IsVisible = false;
            EditPanel.IsVisible = false;
        }
    }

    private void AddAvailability_Clicked(object? sender, EventArgs e)
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
            AvailabilityCollectionView.SelectedItem = slot;
            UpdateValidation();
        }
    }

    private void RemoveAvailability_Clicked(object? sender, EventArgs e)
    {
        if (_selectedTeacher != null && _selectedAvailability != null)
        {
            _selectedTeacher.Availability.Remove(_selectedAvailability);
            _availability.Remove(_selectedAvailability);
            _selectedAvailability = null;
            EditPanel.IsVisible = false;
            UpdateValidation();
        }
    }

    private void StudioPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingEditPanel || _selectedAvailability == null) return;
        
        var studios = AppData.Current.Studios.ToList();
        if (StudioPicker.SelectedIndex >= 0 && StudioPicker.SelectedIndex < studios.Count)
        {
            _selectedAvailability.StudioId = studios[StudioPicker.SelectedIndex].Id;
            RefreshAvailabilityList();
            UpdateValidation();
        }
    }

    private void DayPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingEditPanel || _selectedAvailability == null) return;
        
        if (DayPicker.SelectedIndex >= 0)
        {
            _selectedAvailability.Day = (DayOfWeek)DayPicker.SelectedIndex;
            RefreshAvailabilityList();
            UpdateValidation();
        }
    }

    private void StartTimePicker_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isUpdatingEditPanel || _selectedAvailability == null) return;
        if (e.PropertyName != nameof(TimePicker.Time)) return;
        if (StartTimePicker.Time is not TimeSpan time) return;
        
        _selectedAvailability.StartTime = TimeOnly.FromTimeSpan(time);
        RefreshAvailabilityList();
        UpdateValidation();
    }

    private void EndTimePicker_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isUpdatingEditPanel || _selectedAvailability == null) return;
        if (e.PropertyName != nameof(TimePicker.Time)) return;
        if (EndTimePicker.Time is not TimeSpan time) return;
        
        _selectedAvailability.EndTime = TimeOnly.FromTimeSpan(time);
        RefreshAvailabilityList();
        UpdateValidation();
    }

    private void UpdateValidation()
    {
        if (_selectedTeacher != null)
        {
            _selectedTeacher.NotifyValidationChanged();
            UpdateWorkloadPanel();
            UpdateWarningPanel();
            RefreshTeachersList();
        }
    }

    private void UpdateWorkloadPanel()
    {
        if (_selectedTeacher == null)
        {
            WorkloadPanel.IsVisible = false;
            return;
        }

        WorkloadPanel.IsVisible = true;
        
        GroupsCountLabel.Text = $"{_selectedTeacher.GroupCount} ({FormatDuration(_selectedTeacher.GroupDuration)})";
        SolosCountLabel.Text = $"{_selectedTeacher.SoloCount} ({FormatDuration(_selectedTeacher.SoloDuration)})";
        TotalWorkloadLabel.Text = $"{FormatDuration(_selectedTeacher.TotalWorkload)} / {FormatDuration(_selectedTeacher.TotalAvailability)}";
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
            WarningPanel.IsVisible = true;
            WarningLabel.Text = _selectedTeacher.WarningsSummary;
        }
        else
        {
            WarningPanel.IsVisible = false;
        }
    }

    private void RefreshTeachersList()
    {
        // Force UI refresh by reassigning ItemsSource
        var selected = TeachersCollectionView.SelectedItem;
        TeachersCollectionView.ItemsSource = null;
        TeachersCollectionView.ItemsSource = AppData.Current.Teachers;
        TeachersCollectionView.SelectedItem = selected;
    }

    private void RefreshAvailabilityList()
    {
        // Force UI refresh
        var selected = AvailabilityCollectionView.SelectedItem;
        AvailabilityCollectionView.ItemsSource = null;
        AvailabilityCollectionView.ItemsSource = _availability;
        AvailabilityCollectionView.SelectedItem = selected;
    }
}

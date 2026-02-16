using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using ClassiqueTimetabler.Maui.Data;
using ClassiqueTimetabler.Maui.Models;

namespace ClassiqueTimetabler.Maui.Views;

public partial class GroupsTab : ContentView
{
    private Group? _selectedGroup;
    private bool _isUpdating;
    private bool _groupByTeacher = true;
    private string _searchText = "";
    private ObservableCollection<Group> _filteredGroups = new();

    public GroupsTab()
    {
        InitializeComponent();
        Loaded += GroupsTab_Loaded;
    }

    private void GroupsTab_Loaded(object? sender, EventArgs e)
    {
        GroupByPicker.SelectedIndex = 0;
        StudioPicker.ItemsSource = AppData.Current.Studios;
        
        AppData.Current.Groups.CollectionChanged += Groups_CollectionChanged;
        AppData.Current.Teachers.CollectionChanged += Teachers_CollectionChanged;
        
        foreach (var group in AppData.Current.Groups)
        {
            group.PropertyChanged += Group_PropertyChanged;
        }
        
        RefreshFilteredGroups();
        UpdateEditPanelVisibility();
    }

    private void Groups_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (Group group in e.NewItems)
            {
                group.PropertyChanged += Group_PropertyChanged;
            }
        }
        
        if (e.OldItems != null)
        {
            foreach (Group group in e.OldItems)
            {
                group.PropertyChanged -= Group_PropertyChanged;
            }
        }
        
        MainThread.BeginInvokeOnMainThread(RefreshFilteredGroups);
    }

    private void Teachers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Refresh teacher checkboxes when teachers change
        if (_selectedGroup != null)
        {
            MainThread.BeginInvokeOnMainThread(UpdateTeacherCheckboxes);
        }
    }

    private void Group_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Group.FirstTeacherName) ||
            e.PropertyName == nameof(Group.DayGrouping) ||
            e.PropertyName == nameof(Group.SortableStartTime) ||
            e.PropertyName == nameof(Group.IsFixedTime) ||
            e.PropertyName == nameof(Group.Day))
        {
            MainThread.BeginInvokeOnMainThread(RefreshFilteredGroups);
        }
    }

    private void SearchEntry_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue ?? "";
        RefreshFilteredGroups();
    }

    private void GroupByPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _groupByTeacher = GroupByPicker.SelectedIndex == 0;
        RefreshFilteredGroups();
    }

    private void RefreshFilteredGroups()
    {
        var groups = AppData.Current.Groups.AsEnumerable();
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            groups = groups.Where(g => g.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }
        
        // Apply sorting
        if (_groupByTeacher)
        {
            groups = groups
                .OrderBy(g => g.FirstTeacherName)
                .ThenBy(g => g.Day)
                .ThenBy(g => g.SortableStartTime);
        }
        else
        {
            groups = groups
                .OrderByDescending(g => g.IsFixedTime)
                .ThenBy(g => g.Day)
                .ThenBy(g => g.SortableStartTime)
                .ThenBy(g => g.FirstTeacherName);
        }
        
        _filteredGroups = new ObservableCollection<Group>(groups);
        GroupsCollectionView.ItemsSource = _filteredGroups;
        
        // Restore selection if still valid
        if (_selectedGroup != null && _filteredGroups.Contains(_selectedGroup))
        {
            GroupsCollectionView.SelectedItem = _selectedGroup;
        }
    }

    private void GroupsCollectionView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedGroup = GroupsCollectionView.SelectedItem as Group;
        UpdateEditPanel();
    }

    private void UpdateEditPanelVisibility()
    {
        EditPanel.IsVisible = _selectedGroup != null;
    }

    private void UpdateEditPanel()
    {
        if (_selectedGroup == null)
        {
            UpdateEditPanelVisibility();
            return;
        }

        _isUpdating = true;

        GroupNameEntry.Text = _selectedGroup.Name;
        UpdateTeacherCheckboxes();
        
        // Set studio picker
        var studio = AppData.Current.Studios.FirstOrDefault(s => s.Id == _selectedGroup.StudioId);
        StudioPicker.SelectedItem = studio;
        
        IsFixedTimeCheckBox.IsChecked = _selectedGroup.IsFixedTime;
        DayPicker.SelectedIndex = (int)_selectedGroup.Day;
        StartTimePicker.Time = _selectedGroup.StartTime.ToTimeSpan();
        EndTimePicker.Time = _selectedGroup.EndTime.ToTimeSpan();
        
        // Set duration picker
        var durationIndex = Array.IndexOf((int[])DurationPicker.ItemsSource!, _selectedGroup.DurationMinutes);
        DurationPicker.SelectedIndex = durationIndex >= 0 ? durationIndex : 11; // Default to 60 min

        // Update students display
        StudentsInGroupLabel.Text = _selectedGroup.StudentNamesDisplay;

        // Update warning panel
        UpdateWarningPanel();

        UpdateSchedulePanelVisibility();
        UpdateEditPanelVisibility();

        _isUpdating = false;
    }

    private void UpdateTeacherCheckboxes()
    {
        TeacherCheckboxes.Children.Clear();
        
        foreach (var teacher in AppData.Current.Teachers)
        {
            var isSelected = _selectedGroup?.TeacherIds.Contains(teacher.Id) ?? false;
            
            var checkbox = new CheckBox
            {
                IsChecked = isSelected,
                BindingContext = teacher.Id
            };
            checkbox.CheckedChanged += TeacherCheckbox_CheckedChanged;
            
            var label = new Label
            {
                Text = teacher.Name,
                VerticalOptions = LayoutOptions.Center
            };
            
            var stack = new HorizontalStackLayout
            {
                Spacing = 8,
                Children = { checkbox, label }
            };
            
            TeacherCheckboxes.Children.Add(stack);
        }
    }

    private void TeacherCheckbox_CheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_isUpdating || _selectedGroup == null) return;
        
        if (sender is CheckBox checkbox && checkbox.BindingContext is Guid teacherId)
        {
            if (e.Value && !_selectedGroup.TeacherIds.Contains(teacherId))
            {
                _selectedGroup.TeacherIds.Add(teacherId);
            }
            else if (!e.Value && _selectedGroup.TeacherIds.Contains(teacherId))
            {
                _selectedGroup.TeacherIds.Remove(teacherId);
            }
            
            _selectedGroup.NotifyTeacherChanged();
            UpdateWarningPanel();
            RefreshFilteredGroups();
        }
    }

    private void UpdateWarningPanel()
    {
        if (_selectedGroup != null && _selectedGroup.HasWarnings)
        {
            WarningPanel.IsVisible = true;
            WarningText.Text = _selectedGroup.WarningsSummary;
        }
        else
        {
            WarningPanel.IsVisible = false;
        }
    }

    private void UpdateSchedulePanelVisibility()
    {
        bool isFixed = IsFixedTimeCheckBox.IsChecked;
        SchedulePanel.IsVisible = isFixed;
        StudioPanel.IsVisible = isFixed;
        DurationPanel.IsVisible = !isFixed;
    }

    private void GroupNameEntry_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdating || _selectedGroup == null) return;
        _selectedGroup.Name = e.NewTextValue ?? "";
    }

    private void StudioPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdating || _selectedGroup == null) return;
        
        if (StudioPicker.SelectedItem is Studio studio)
        {
            _selectedGroup.StudioId = studio.Id;
        }
        else
        {
            _selectedGroup.StudioId = null;
        }
        UpdateWarningPanel();
    }

    private void IsFixedTimeCheckBox_CheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_isUpdating || _selectedGroup == null) return;
        _selectedGroup.IsFixedTime = e.Value;
        UpdateSchedulePanelVisibility();
        UpdateWarningPanel();
        RefreshFilteredGroups();
    }

    private void DayPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdating || _selectedGroup == null) return;
        if (DayPicker.SelectedIndex >= 0)
        {
            _selectedGroup.Day = (DayOfWeek)DayPicker.SelectedIndex;
        }
    }

    private void DurationPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdating || _selectedGroup == null) return;
        if (DurationPicker.SelectedItem is int minutes)
        {
            _selectedGroup.DurationMinutes = minutes;
        }
    }

    private void StartTimePicker_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimePicker.Time)) return;
        if (_isUpdating || _selectedGroup == null) return;
        if (StartTimePicker.Time is TimeSpan time)
        {
            _selectedGroup.StartTime = TimeOnly.FromTimeSpan(time);
            UpdateWarningPanel();
        }
    }

    private void EndTimePicker_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimePicker.Time)) return;
        if (_isUpdating || _selectedGroup == null) return;
        if (EndTimePicker.Time is TimeSpan time)
        {
            _selectedGroup.EndTime = TimeOnly.FromTimeSpan(time);
            UpdateWarningPanel();
        }
    }

    private void AddGroup_Clicked(object? sender, EventArgs e)
    {
        var group = new Group
        {
            Name = "New Group",
            IsFixedTime = false,
            DurationMinutes = 60
        };
        AppData.Current.Groups.Add(group);
        RefreshFilteredGroups();
        GroupsCollectionView.SelectedItem = group;
    }

    private void RemoveGroup_Clicked(object? sender, EventArgs e)
    {
        if (_selectedGroup != null)
        {
            AppData.Current.Groups.Remove(_selectedGroup);
            _selectedGroup = null;
            RefreshFilteredGroups();
            UpdateEditPanel();
        }
    }
}

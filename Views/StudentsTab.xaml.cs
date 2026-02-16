using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using ClassiqueTimetabler.Maui.Data;
using ClassiqueTimetabler.Maui.Models;

namespace ClassiqueTimetabler.Maui.Views;

public partial class StudentsTab : ContentView
{
    private Student? _selectedStudent;
    private StudentSolo? _selectedSolo;
    private StudentUnavailability? _selectedUnavailability;
    private ObservableCollection<Student> _filteredStudents = new();
    private ObservableCollection<StudentSolo> _solos = new();
    private ObservableCollection<StudentUnavailability> _unavailability = new();
    private bool _isUpdating;
    private string _searchText = "";

    public StudentsTab()
    {
        InitializeComponent();
        SolosCollectionView.ItemsSource = _solos;
        UnavailabilityCollectionView.ItemsSource = _unavailability;
        SoloTeacherPicker.ItemsSource = AppData.Current.Teachers;
        Loaded += StudentsTab_Loaded;
    }

    private void StudentsTab_Loaded(object? sender, EventArgs e)
    {
        AppData.Current.Groups.CollectionChanged += Groups_CollectionChanged;
        RefreshFilteredStudents();
        UpdateEditPanelVisibility();
    }

    private void Groups_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_selectedStudent != null)
        {
            MainThread.BeginInvokeOnMainThread(UpdateGroupCheckboxes);
        }
    }

    private void SearchEntry_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue ?? "";
        RefreshFilteredStudents();
    }

    private void RefreshFilteredStudents()
    {
        var students = AppData.Current.Students.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            students = students.Where(s => s.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        students = students.OrderBy(s => s.Name);

        _filteredStudents = new ObservableCollection<Student>(students);
        StudentsCollectionView.ItemsSource = _filteredStudents;

        if (_selectedStudent != null && _filteredStudents.Contains(_selectedStudent))
        {
            StudentsCollectionView.SelectedItem = _selectedStudent;
        }
    }

    private void StudentsCollectionView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedStudent = StudentsCollectionView.SelectedItem as Student;
        _selectedSolo = null;
        _selectedUnavailability = null;
        UpdateEditPanel();
    }

    private void UpdateEditPanelVisibility()
    {
        EditPanel.IsVisible = _selectedStudent != null;
    }

    private void UpdateEditPanel()
    {
        if (_selectedStudent == null)
        {
            UpdateEditPanelVisibility();
            return;
        }

        _isUpdating = true;

        StudentNameEntry.Text = _selectedStudent.Name;
        YearOfBirthEntry.Text = _selectedStudent.YearOfBirth.ToString();
        UpdateAgeDisplay();
        UpdateGroupCheckboxes();
        UpdateGroupNamesDisplay();

        // Load solos
        _solos.Clear();
        foreach (var solo in _selectedStudent.Solos)
        {
            _solos.Add(solo);
        }

        // Load unavailability
        _unavailability.Clear();
        foreach (var slot in _selectedStudent.Unavailability)
        {
            _unavailability.Add(slot);
        }

        SoloEditPanel.IsVisible = false;
        UnavailabilityEditPanel.IsVisible = false;
        UpdateEditPanelVisibility();

        _isUpdating = false;
    }

    private void UpdateAgeDisplay()
    {
        if (_selectedStudent != null)
        {
            AgeDisplay.Text = $"(Age: {_selectedStudent.Age})";
        }
    }

    private void UpdateGroupCheckboxes()
    {
        GroupCheckboxes.Children.Clear();

        foreach (var group in AppData.Current.Groups.OrderBy(g => g.Name))
        {
            var isSelected = _selectedStudent?.GroupIds.Contains(group.Id) ?? false;

            var checkbox = new CheckBox
            {
                IsChecked = isSelected,
                BindingContext = group.Id
            };
            checkbox.CheckedChanged += GroupCheckbox_CheckedChanged;

            var label = new Label
            {
                Text = group.Name,
                VerticalOptions = LayoutOptions.Center
            };

            var stack = new HorizontalStackLayout
            {
                Spacing = 8,
                Children = { checkbox, label }
            };

            GroupCheckboxes.Children.Add(stack);
        }
    }

    private void GroupCheckbox_CheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_isUpdating || _selectedStudent == null) return;

        if (sender is CheckBox checkbox && checkbox.BindingContext is Guid groupId)
        {
            if (e.Value && !_selectedStudent.GroupIds.Contains(groupId))
            {
                _selectedStudent.GroupIds.Add(groupId);
                // Notify the group that student count changed
                var group = AppData.Current.Groups.FirstOrDefault(g => g.Id == groupId);
                group?.NotifyStudentsChanged();
            }
            else if (!e.Value && _selectedStudent.GroupIds.Contains(groupId))
            {
                _selectedStudent.GroupIds.Remove(groupId);
                var group = AppData.Current.Groups.FirstOrDefault(g => g.Id == groupId);
                group?.NotifyStudentsChanged();
            }

            _selectedStudent.NotifyGroupsChanged();
            UpdateGroupNamesDisplay();
            RefreshFilteredStudents();
        }
    }

    private void UpdateGroupNamesDisplay()
    {
        if (_selectedStudent == null || _selectedStudent.GroupIds.Count == 0)
        {
            GroupNamesDisplay.Text = "No groups selected";
        }
        else
        {
            var groupNames = _selectedStudent.GroupIds
                .Select(id => AppData.Current.Groups.FirstOrDefault(g => g.Id == id)?.Name)
                .Where(name => name != null)
                .OrderBy(name => name);
            GroupNamesDisplay.Text = string.Join(", ", groupNames);
        }
    }

    private void StudentNameEntry_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdating || _selectedStudent == null) return;
        _selectedStudent.Name = e.NewTextValue ?? "";
        RefreshFilteredStudents();
    }

    private void YearOfBirthEntry_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdating || _selectedStudent == null) return;
        if (int.TryParse(e.NewTextValue, out int year))
        {
            _selectedStudent.YearOfBirth = year;
            UpdateAgeDisplay();
            RefreshFilteredStudents();
        }
    }

    private void AddStudent_Clicked(object? sender, EventArgs e)
    {
        var student = new Student
        {
            Name = "New Student",
            YearOfBirth = DateTime.Now.Year - 10
        };
        AppData.Current.Students.Add(student);
        RefreshFilteredStudents();
        StudentsCollectionView.SelectedItem = student;
    }

    private void RemoveStudent_Clicked(object? sender, EventArgs e)
    {
        if (_selectedStudent != null)
        {
            // Remove student from all groups' student counts
            foreach (var groupId in _selectedStudent.GroupIds)
            {
                var group = AppData.Current.Groups.FirstOrDefault(g => g.Id == groupId);
                group?.NotifyStudentsChanged();
            }

            AppData.Current.Students.Remove(_selectedStudent);
            _selectedStudent = null;
            _solos.Clear();
            _unavailability.Clear();
            RefreshFilteredStudents();
            UpdateEditPanel();
        }
    }

    #region Solos

    private void SolosCollectionView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedSolo = SolosCollectionView.SelectedItem as StudentSolo;
        UpdateSoloEditPanel();
    }

    private void UpdateSoloEditPanel()
    {
        if (_selectedSolo == null)
        {
            SoloEditPanel.IsVisible = false;
            return;
        }

        _isUpdating = true;

        SoloNameEntry.Text = _selectedSolo.Name;

        var durationList = (int[])SoloDurationPicker.ItemsSource;
        var durationIndex = Array.IndexOf(durationList, _selectedSolo.DurationMinutes);
        SoloDurationPicker.SelectedIndex = durationIndex >= 0 ? durationIndex : 1; // Default to 10

        var teacher = AppData.Current.Teachers.FirstOrDefault(t => t.Id == _selectedSolo.TeacherId);
        SoloTeacherPicker.SelectedItem = teacher;

        SoloEditPanel.IsVisible = true;

        _isUpdating = false;
    }

    private void SoloNameEntry_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdating || _selectedSolo == null) return;
        _selectedSolo.Name = e.NewTextValue ?? "";
    }

    private void SoloDurationPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdating || _selectedSolo == null) return;
        if (SoloDurationPicker.SelectedItem is int minutes)
        {
            _selectedSolo.DurationMinutes = minutes;
        }
    }

    private void SoloTeacherPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdating || _selectedSolo == null) return;
        if (SoloTeacherPicker.SelectedItem is Teacher teacher)
        {
            _selectedSolo.TeacherId = teacher.Id;
        }
    }

    private void AddSolo_Clicked(object? sender, EventArgs e)
    {
        if (_selectedStudent != null)
        {
            var firstTeacher = AppData.Current.Teachers.FirstOrDefault();
            var solo = new StudentSolo
            {
                Name = "New Solo",
                DurationMinutes = 10,
                TeacherId = firstTeacher?.Id
            };
            _selectedStudent.Solos.Add(solo);
            _solos.Add(solo);
            SolosCollectionView.SelectedItem = solo;
            RefreshFilteredStudents();
        }
    }

    private void RemoveSolo_Clicked(object? sender, EventArgs e)
    {
        if (_selectedStudent != null && _selectedSolo != null)
        {
            _selectedStudent.Solos.Remove(_selectedSolo);
            _solos.Remove(_selectedSolo);
            _selectedSolo = null;
            SoloEditPanel.IsVisible = false;
            RefreshFilteredStudents();
        }
    }

    #endregion

    #region Unavailability

    private void UnavailabilityCollectionView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedUnavailability = UnavailabilityCollectionView.SelectedItem as StudentUnavailability;
        UpdateUnavailabilityEditPanel();
    }

    private void UpdateUnavailabilityEditPanel()
    {
        if (_selectedUnavailability == null)
        {
            UnavailabilityEditPanel.IsVisible = false;
            return;
        }

        _isUpdating = true;

        UnavailabilityDayPicker.SelectedIndex = (int)_selectedUnavailability.Day;
        UnavailabilityStartPicker.Time = _selectedUnavailability.StartTime.ToTimeSpan();
        UnavailabilityEndPicker.Time = _selectedUnavailability.EndTime.ToTimeSpan();

        UnavailabilityEditPanel.IsVisible = true;

        _isUpdating = false;
    }

    private void UnavailabilityDayPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdating || _selectedUnavailability == null) return;
        if (UnavailabilityDayPicker.SelectedIndex >= 0)
        {
            _selectedUnavailability.Day = (DayOfWeek)UnavailabilityDayPicker.SelectedIndex;
        }
    }

    private void UnavailabilityStartPicker_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimePicker.Time)) return;
        if (_isUpdating || _selectedUnavailability == null) return;
        if (UnavailabilityStartPicker.Time is TimeSpan time)
        {
            _selectedUnavailability.StartTime = TimeOnly.FromTimeSpan(time);
        }
    }

    private void UnavailabilityEndPicker_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimePicker.Time)) return;
        if (_isUpdating || _selectedUnavailability == null) return;
        if (UnavailabilityEndPicker.Time is TimeSpan time)
        {
            _selectedUnavailability.EndTime = TimeOnly.FromTimeSpan(time);
        }
    }

    private void AddUnavailability_Clicked(object? sender, EventArgs e)
    {
        if (_selectedStudent != null)
        {
            var slot = new StudentUnavailability
            {
                Day = DayOfWeek.Monday,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0)
            };
            _selectedStudent.Unavailability.Add(slot);
            _unavailability.Add(slot);
            UnavailabilityCollectionView.SelectedItem = slot;
        }
    }

    private void RemoveUnavailability_Clicked(object? sender, EventArgs e)
    {
        if (_selectedStudent != null && _selectedUnavailability != null)
        {
            _selectedStudent.Unavailability.Remove(_selectedUnavailability);
            _unavailability.Remove(_selectedUnavailability);
            _selectedUnavailability = null;
            UnavailabilityEditPanel.IsVisible = false;
        }
    }

    #endregion
}

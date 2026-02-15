using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using classique.timetabler.Data;
using classique.timetabler.Models;

namespace classique.timetabler.Tabs.Groups
{
    public partial class GroupsTab : UserControl
    {
        private Group? _selectedGroup;
        private bool _isUpdating;
        private bool _groupByTeacher = true;
        private string _searchText = "";

        public GroupsTab()
        {
            InitializeComponent();
            Loaded += GroupsTab_Loaded;
            
            // Listen for teacher selection changes
            AddHandler(Controls.MultiSelectTeacherComboBox.SelectionChangedEvent, 
                new RoutedEventHandler(OnTeacherSelectionChanged));
        }

        private void GroupsTab_Loaded(object sender, RoutedEventArgs e)
        {
            StudioComboBox.ItemsSource = AppData.Current.Studios;
            AppData.Current.Groups.CollectionChanged += Groups_CollectionChanged;
            
            // Subscribe to property changes on existing groups
            foreach (var group in AppData.Current.Groups)
            {
                group.PropertyChanged += Group_PropertyChanged;
            }
            
            ApplyGrouping();
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
            
            Dispatcher.BeginInvoke(new Action(() => RefreshView()), 
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void Group_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Refresh when properties that affect grouping/sorting change
            if (e.PropertyName == nameof(Group.FirstTeacherName) || 
                e.PropertyName == nameof(Group.DayGrouping) ||
                e.PropertyName == nameof(Group.SortableStartTime) ||
                e.PropertyName == nameof(Group.IsFixedTime) ||
                e.PropertyName == nameof(Group.Day))
            {
                Dispatcher.BeginInvoke(new Action(() => RefreshView()), 
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = SearchTextBox.Text;
            ApplyFilter();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
        }

        private void ApplyFilter()
        {
            var view = CollectionViewSource.GetDefaultView(GroupsListBox.ItemsSource);
            if (view == null) return;

            if (string.IsNullOrWhiteSpace(_searchText))
            {
                view.Filter = null;
            }
            else
            {
                view.Filter = obj =>
                {
                    if (obj is Group group)
                    {
                        return group.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
                    }
                    return false;
                };
            }

            view.Refresh();
        }

        private void GroupByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            
            _groupByTeacher = GroupByComboBox.SelectedIndex == 0;
            ApplyGrouping();
        }

        private void ApplyGrouping()
        {
            var view = CollectionViewSource.GetDefaultView(GroupsListBox.ItemsSource);
            if (view == null) return;

            view.GroupDescriptions.Clear();
            view.SortDescriptions.Clear();

            if (_groupByTeacher)
            {
                // Group by teacher, sort by day then start time (flexible at end)
                view.GroupDescriptions.Add(new PropertyGroupDescription("FirstTeacherName"));
                view.SortDescriptions.Add(new SortDescription("FirstTeacherName", ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription("Day", ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription("SortableStartTime", ListSortDirection.Ascending));
            }
            else
            {
                // Group by day (with Flexible as a group), sort by time then teacher
                view.GroupDescriptions.Add(new PropertyGroupDescription("DayGrouping"));
                view.SortDescriptions.Add(new SortDescription("IsFixedTime", ListSortDirection.Descending)); // Fixed first
                view.SortDescriptions.Add(new SortDescription("Day", ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription("SortableStartTime", ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription("FirstTeacherName", ListSortDirection.Ascending));
            }

            // Reapply filter
            ApplyFilter();
        }

        private void RefreshView()
        {
            var view = CollectionViewSource.GetDefaultView(GroupsListBox.ItemsSource);
            if (view != null)
            {
                view.Refresh();
            }
        }

        private void GroupsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedGroup = GroupsListBox.SelectedItem as Group;
            UpdateEditPanel();
        }

        private void UpdateEditPanelVisibility()
        {
            EditPanel.Visibility = _selectedGroup != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateEditPanel()
        {
            if (_selectedGroup == null)
            {
                UpdateEditPanelVisibility();
                return;
            }

            _isUpdating = true;

            GroupNameTextBox.Text = _selectedGroup.Name;
            TeachersComboBox.SelectedTeacherIds = _selectedGroup.TeacherIds;
            StudioComboBox.SelectedValue = _selectedGroup.StudioId;
            IsFixedTimeCheckBox.IsChecked = _selectedGroup.IsFixedTime;
            DayComboBox.SelectedItem = _selectedGroup.Day;
            StartTimePicker.Time = _selectedGroup.StartTime;
            EndTimePicker.Time = _selectedGroup.EndTime;
            
            // Set duration combobox
            SelectDurationItem(_selectedGroup.DurationMinutes);

            // Update students display
            UpdateStudentsDisplay();

            // Update warning panel
            UpdateWarningPanel();

            UpdateSchedulePanelVisibility();
            UpdateEditPanelVisibility();

            _isUpdating = false;
        }

        private void UpdateStudentsDisplay()
        {
            if (_selectedGroup == null)
            {
                StudentsInGroupText.Text = "";
                return;
            }

            StudentsInGroupText.Text = _selectedGroup.StudentNamesDisplay;
        }

        private void UpdateWarningPanel()
        {
            if (_selectedGroup != null && _selectedGroup.HasWarnings)
            {
                WarningPanel.Visibility = Visibility.Visible;
                WarningText.Text = _selectedGroup.WarningsSummary;
            }
            else
            {
                WarningPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void SelectDurationItem(int minutes)
        {
            foreach (ComboBoxItem item in DurationComboBox.Items)
            {
                if (item.Tag is string tagStr && int.TryParse(tagStr, out int tagMinutes) && tagMinutes == minutes)
                {
                    DurationComboBox.SelectedItem = item;
                    return;
                }
            }
            // Default to 60 if not found (index 11 in the 5-minute interval list)
            DurationComboBox.SelectedIndex = 11;
        }

        private void UpdateSchedulePanelVisibility()
        {
            bool isFixed = IsFixedTimeCheckBox.IsChecked == true;
            SchedulePanel.Visibility = isFixed ? Visibility.Visible : Visibility.Collapsed;
            StudioPanel.Visibility = isFixed ? Visibility.Visible : Visibility.Collapsed;
            DurationPanel.Visibility = isFixed ? Visibility.Collapsed : Visibility.Visible;
        }

        private void GroupNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || _selectedGroup == null) return;
            _selectedGroup.Name = GroupNameTextBox.Text;
        }

        private void OnTeacherSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _selectedGroup == null) return;
            _selectedGroup.NotifyTeacherChanged();
            UpdateWarningPanel();
            RefreshView();
        }

        private void TeachersComboBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // Handled by OnTeacherSelectionChanged
        }

        private void StudioComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating || _selectedGroup == null) return;
            _selectedGroup.StudioId = StudioComboBox.SelectedValue as Guid?;
            UpdateWarningPanel();
        }

        private void IsFixedTimeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _selectedGroup == null) return;
            _selectedGroup.IsFixedTime = IsFixedTimeCheckBox.IsChecked == true;
            UpdateSchedulePanelVisibility();
            UpdateWarningPanel();
            RefreshView();
        }

        private void DayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating || _selectedGroup == null) return;
            if (DayComboBox.SelectedItem is DayOfWeek day)
            {
                _selectedGroup.Day = day;
            }
        }

        private void DurationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating || _selectedGroup == null) return;
            if (DurationComboBox.SelectedItem is ComboBoxItem item && 
                item.Tag is string tagStr && 
                int.TryParse(tagStr, out int minutes))
            {
                _selectedGroup.DurationMinutes = minutes;
            }
        }

        private void StartTimePicker_TimeChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _selectedGroup == null) return;
            _selectedGroup.StartTime = StartTimePicker.Time;
            UpdateWarningPanel();
        }

        private void EndTimePicker_TimeChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _selectedGroup == null) return;
            _selectedGroup.EndTime = EndTimePicker.Time;
            UpdateWarningPanel();
        }

        private void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            var group = new Group 
            { 
                Name = "New Group",
                IsFixedTime = false,
                DurationMinutes = 60
            };
            AppData.Current.Groups.Add(group);
            GroupsListBox.SelectedItem = group;
        }

        private void RemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGroup != null)
            {
                AppData.Current.Groups.Remove(_selectedGroup);
                _selectedGroup = null;
                UpdateEditPanel();
            }
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int length)
            {
                return length == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LengthToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int length)
            {
                return length > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

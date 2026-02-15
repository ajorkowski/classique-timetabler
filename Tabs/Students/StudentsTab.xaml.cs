using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using classique.timetabler.Data;
using classique.timetabler.Models;

namespace classique.timetabler.Tabs.Students
{
    public partial class StudentsTab : UserControl
    {
        private Student? _selectedStudent;
        private ObservableCollection<StudentSolo> _solos = new();
        private ObservableCollection<StudentUnavailability> _unavailability = new();
        private bool _isUpdating;

        public ObservableCollection<Teacher> Teachers => AppData.Current.Teachers;

        public StudentsTab()
        {
            InitializeComponent();
            SolosDataGrid.ItemsSource = _solos;
            UnavailabilityDataGrid.ItemsSource = _unavailability;
            Loaded += StudentsTab_Loaded;
            
            // Listen for group selection changes
            AddHandler(Controls.MultiSelectGroupComboBox.SelectionChangedEvent, 
                new RoutedEventHandler(OnGroupSelectionChanged));
        }

        private void StudentsTab_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateEditPanelVisibility();
        }

        private void StudentsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedStudent = StudentsListBox.SelectedItem as Student;
            UpdateEditPanel();
        }

        private void UpdateEditPanelVisibility()
        {
            EditPanel.Visibility = _selectedStudent != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateEditPanel()
        {
            if (_selectedStudent == null)
            {
                UpdateEditPanelVisibility();
                return;
            }

            _isUpdating = true;

            StudentNameTextBox.Text = _selectedStudent.Name;
            YearOfBirthTextBox.Text = _selectedStudent.YearOfBirth.ToString();
            UpdateAgeDisplay();
            GroupsComboBox.SelectedGroupIds = _selectedStudent.GroupIds;

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

        private void StudentNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || _selectedStudent == null) return;
            _selectedStudent.Name = StudentNameTextBox.Text;
            RefreshListBox();
        }

        private void YearOfBirthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || _selectedStudent == null) return;
            if (int.TryParse(YearOfBirthTextBox.Text, out int year))
            {
                _selectedStudent.YearOfBirth = year;
                UpdateAgeDisplay();
                RefreshListBox();
            }
        }

        private void OnGroupSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _selectedStudent == null) return;
            _selectedStudent.NotifyGroupsChanged();
            RefreshListBox();
        }

        private void AddStudent_Click(object sender, RoutedEventArgs e)
        {
            var student = new Student 
            { 
                Name = "New Student",
                YearOfBirth = DateTime.Now.Year - 10
            };
            AppData.Current.Students.Add(student);
            StudentsListBox.SelectedItem = student;
        }

        private void RemoveStudent_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedStudent != null)
            {
                AppData.Current.Students.Remove(_selectedStudent);
                _selectedStudent = null;
                _solos.Clear();
                _unavailability.Clear();
                UpdateEditPanel();
            }
        }

        private void AddSolo_Click(object sender, RoutedEventArgs e)
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
                RefreshListBox();
            }
        }

        private void RemoveSolo_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedStudent != null && SolosDataGrid.SelectedItem is StudentSolo solo)
            {
                _selectedStudent.Solos.Remove(solo);
                _solos.Remove(solo);
                RefreshListBox();
            }
        }

        private void AddUnavailability_Click(object sender, RoutedEventArgs e)
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
            }
        }

        private void RemoveUnavailability_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedStudent != null && UnavailabilityDataGrid.SelectedItem is StudentUnavailability slot)
            {
                _selectedStudent.Unavailability.Remove(slot);
                _unavailability.Remove(slot);
            }
        }

        private void RefreshListBox()
        {
            var index = StudentsListBox.SelectedIndex;
            StudentsListBox.Items.Refresh();
            StudentsListBox.SelectedIndex = index;
        }
    }
}

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using classique.timetabler.Data;
using classique.timetabler.Models;

namespace classique.timetabler.Controls
{
    public partial class ScheduleResultsView : UserControl
    {
        private List<ScheduledClass> _scheduledClasses = [];

        public ScheduleResultsView()
        {
            InitializeComponent();
        }

        public void SetScheduledClasses(List<ScheduledClass> scheduledClasses)
        {
            _scheduledClasses = scheduledClasses;
            UpdateSummary();
            PopulateStudentFilter();
            RefreshView();
        }

        private void UpdateSummary()
        {
            var groupCount = _scheduledClasses.Count(c => !c.IsSolo);
            var soloCount = _scheduledClasses.Count(c => c.IsSolo);
            
            SummaryText.Text = $"{_scheduledClasses.Count} classes scheduled";
            GroupsCountText.Text = $"{groupCount} groups";
            SolosCountText.Text = $"{soloCount} solos";
        }

        private void PopulateStudentFilter()
        {
            var students = AppData.Current.Students
                .OrderBy(s => s.Name)
                .ToList();

            StudentFilterComboBox.Items.Clear();
            StudentFilterComboBox.Items.Add(new ComboBoxItem { Content = "All Students", Tag = null });
            
            foreach (var student in students)
            {
                StudentFilterComboBox.Items.Add(new ComboBoxItem { Content = student.Name, Tag = student.Id });
            }
            
            StudentFilterComboBox.SelectedIndex = 0;
        }

        private void ViewModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StudentFilterLabel == null) return;

            var selectedItem = ViewModeComboBox.SelectedItem as ComboBoxItem;
            var viewMode = selectedItem?.Content?.ToString() ?? "Day";

            // Show/hide student filter
            var showStudentFilter = viewMode == "Student";
            StudentFilterLabel.Visibility = showStudentFilter ? Visibility.Visible : Visibility.Collapsed;
            StudentFilterComboBox.Visibility = showStudentFilter ? Visibility.Visible : Visibility.Collapsed;

            RefreshView();
        }

        private void StudentFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshView();
        }

        private void RefreshView()
        {
            if (ScheduleItemsControl == null || _scheduledClasses.Count == 0) return;

            var selectedItem = ViewModeComboBox.SelectedItem as ComboBoxItem;
            var viewMode = selectedItem?.Content?.ToString() ?? "Day";

            var groups = viewMode switch
            {
                "Day" => GroupByDay(),
                "Teacher" => GroupByTeacher(),
                "Studio" => GroupByStudio(),
                "Student" => GroupByStudent(),
                _ => GroupByDay()
            };

            ScheduleItemsControl.ItemsSource = groups;
        }

        private List<ScheduleGroup> GroupByDay()
        {
            var dayOrder = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
                                   DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

            return _scheduledClasses
                .GroupBy(c => c.Day)
                .OrderBy(g => Array.IndexOf(dayOrder, g.Key))
                .Select(dayGroup => new ScheduleGroup
                {
                    GroupHeader = dayGroup.Key.ToString(),
                    SubGroups = dayGroup
                        .GroupBy(c => c.TeacherId)
                        .OrderBy(g => GetTeacherName(g.Key))
                        .Select(teacherGroup => new ScheduleSubGroup
                        {
                            SubGroupHeader = GetTeacherName(teacherGroup.Key),
                            Classes = teacherGroup
                                .OrderBy(c => c.StartTime)
                                .Select(c => CreateClassViewModel(c, showTeacher: false))
                                .ToList()
                        })
                        .ToList()
                })
                .ToList();
        }

        private List<ScheduleGroup> GroupByTeacher()
        {
            var dayOrder = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
                                   DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

            return _scheduledClasses
                .GroupBy(c => c.TeacherId)
                .OrderBy(g => GetTeacherName(g.Key))
                .Select(teacherGroup => new ScheduleGroup
                {
                    GroupHeader = GetTeacherName(teacherGroup.Key),
                    SubGroups = teacherGroup
                        .GroupBy(c => c.Day)
                        .OrderBy(g => Array.IndexOf(dayOrder, g.Key))
                        .Select(dayGroup => new ScheduleSubGroup
                        {
                            SubGroupHeader = dayGroup.Key.ToString(),
                            Classes = dayGroup
                                .OrderBy(c => c.StartTime)
                                .Select(c => CreateClassViewModel(c, showTeacher: false))
                                .ToList()
                        })
                        .ToList()
                })
                .ToList();
        }

        private List<ScheduleGroup> GroupByStudio()
        {
            var dayOrder = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
                                   DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

            return _scheduledClasses
                .GroupBy(c => c.StudioId)
                .OrderBy(g => GetStudioName(g.Key))
                .Select(studioGroup => new ScheduleGroup
                {
                    GroupHeader = GetStudioName(studioGroup.Key),
                    SubGroups = studioGroup
                        .GroupBy(c => c.Day)
                        .OrderBy(g => Array.IndexOf(dayOrder, g.Key))
                        .Select(dayGroup => new ScheduleSubGroup
                        {
                            SubGroupHeader = dayGroup.Key.ToString(),
                            Classes = dayGroup
                                .OrderBy(c => c.StartTime)
                                .Select(c => CreateClassViewModel(c, showStudio: false))
                                .ToList()
                        })
                        .ToList()
                })
                .ToList();
        }

        private List<ScheduleGroup> GroupByStudent()
        {
            var selectedStudentItem = StudentFilterComboBox.SelectedItem as ComboBoxItem;
            var selectedStudentId = selectedStudentItem?.Tag as Guid?;

            var filteredClasses = selectedStudentId.HasValue
                ? _scheduledClasses.Where(c => c.StudentIds.Contains(selectedStudentId.Value)).ToList()
                : _scheduledClasses;

            var dayOrder = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
                                   DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

            if (selectedStudentId.HasValue)
            {
                // Single student view - group by day only (no subgroups needed)
                return filteredClasses
                    .GroupBy(c => c.Day)
                    .OrderBy(g => Array.IndexOf(dayOrder, g.Key))
                    .Select(g => new ScheduleGroup
                    {
                        GroupHeader = g.Key.ToString(),
                        SubGroups =
                        [
                            new ScheduleSubGroup
                            {
                                SubGroupHeader = null,
                                Classes = g.OrderBy(c => c.StartTime)
                                          .Select(c => CreateClassViewModel(c))
                                          .ToList()
                            }
                        ]
                    })
                    .ToList();
            }
            else
            {
                // All students - group by student, subgroup by day
                var studentClasses = new Dictionary<Guid, List<ScheduledClass>>();
                
                foreach (var scheduledClass in _scheduledClasses)
                {
                    foreach (var studentId in scheduledClass.StudentIds)
                    {
                        if (!studentClasses.ContainsKey(studentId))
                            studentClasses[studentId] = [];
                        studentClasses[studentId].Add(scheduledClass);
                    }
                }

                return studentClasses
                    .OrderBy(kvp => GetStudentName(kvp.Key))
                    .Select(kvp => new ScheduleGroup
                    {
                        GroupHeader = GetStudentName(kvp.Key),
                        SubGroups = kvp.Value
                            .GroupBy(c => c.Day)
                            .OrderBy(g => Array.IndexOf(dayOrder, g.Key))
                            .Select(dayGroup => new ScheduleSubGroup
                            {
                                SubGroupHeader = dayGroup.Key.ToString(),
                                Classes = dayGroup
                                    .OrderBy(c => c.StartTime)
                                    .Select(c => CreateClassViewModel(c))
                                    .ToList()
                            })
                            .ToList()
                    })
                    .ToList();
            }
        }

        private ClassViewModel CreateClassViewModel(ScheduledClass c, bool showTeacher = true, bool showStudio = true)
        {
            return new ClassViewModel
            {
                TimeRange = $"{c.StartTime:HH:mm}-{c.EndTime:HH:mm}",
                ClassName = GetClassName(c),
                StudentNames = GetStudentNames(c),
                TeacherName = showTeacher ? GetTeacherName(c.TeacherId) : null,
                StudioName = showStudio ? GetStudioName(c.StudioId) : null
            };
        }

        private string GetClassName(ScheduledClass c)
        {
            if (c.IsSolo)
            {
                var student = AppData.Current.Students.FirstOrDefault(s => s.Solos.Any(solo => solo.Id == c.SoloId));
                var solo = student?.Solos.FirstOrDefault(s => s.Id == c.SoloId);
                return solo?.Name ?? "Solo";
            }
            else
            {
                var group = AppData.Current.Groups.FirstOrDefault(g => g.Id == c.GroupId);
                return group?.Name ?? "Group";
            }
        }

        private string GetStudentNames(ScheduledClass c)
        {
            var names = c.StudentIds
                .Select(id => AppData.Current.Students.FirstOrDefault(s => s.Id == id)?.Name ?? "Unknown")
                .OrderBy(n => n)
                .ToList();

            if (names.Count == 0) return "";
            if (names.Count <= 3) return string.Join(", ", names);
            return $"{string.Join(", ", names.Take(3))} +{names.Count - 3} more";
        }

        private string GetTeacherName(Guid teacherId)
        {
            return AppData.Current.Teachers.FirstOrDefault(t => t.Id == teacherId)?.Name ?? "Unknown";
        }

        private string GetStudioName(Guid studioId)
        {
            return AppData.Current.Studios.FirstOrDefault(s => s.Id == studioId)?.Name ?? "Unknown";
        }

        private string GetStudentName(Guid studentId)
        {
            return AppData.Current.Students.FirstOrDefault(s => s.Id == studentId)?.Name ?? "Unknown";
        }
    }

    public class ScheduleGroup
    {
        public string GroupHeader { get; set; } = "";
        public List<ScheduleSubGroup> SubGroups { get; set; } = [];
    }

    public class ScheduleSubGroup
    {
        public string? SubGroupHeader { get; set; }
        public List<ClassViewModel> Classes { get; set; } = [];
    }

    public class ClassViewModel
    {
        public string TimeRange { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string StudentNames { get; set; } = "";
        public string? TeacherName { get; set; }
        public string? StudioName { get; set; }
    }

    public class NullToCollapsedConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

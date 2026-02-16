using ClassiqueTimetabler.Maui.Data;
using ClassiqueTimetabler.Maui.Models;
using Microsoft.Maui.Controls.Shapes;

namespace ClassiqueTimetabler.Maui.Views;

public partial class ScheduleResultsView : ContentView
{
    private List<ScheduledClass> _scheduledClasses = [];
    private List<StudentItem> _studentItems = [];

    public ScheduleResultsView()
    {
        InitializeComponent();
        ViewModePicker.SelectedIndex = 0;
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
        _studentItems = AppData.Current.Students
            .OrderBy(s => s.Name)
            .Select(s => new StudentItem { Id = s.Id, Name = s.Name })
            .ToList();

        _studentItems.Insert(0, new StudentItem { Id = null, Name = "All Students" });

        StudentFilterPicker.ItemsSource = _studentItems;
        StudentFilterPicker.ItemDisplayBinding = new Binding("Name");
        StudentFilterPicker.SelectedIndex = 0;
    }

    private void ViewModePicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (StudentFilterLabel == null) return;

        var viewMode = ViewModePicker.SelectedItem?.ToString() ?? "Day";

        // Show/hide student filter
        var showStudentFilter = viewMode == "Student";
        StudentFilterLabel.IsVisible = showStudentFilter;
        StudentFilterPicker.IsVisible = showStudentFilter;

        RefreshView();
    }

    private void StudentFilterPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        RefreshView();
    }

    private void RefreshView()
    {
        if (ScheduleContainer == null || _scheduledClasses.Count == 0)
        {
            ScheduleContainer?.Children.Clear();
            return;
        }

        var viewMode = ViewModePicker.SelectedItem?.ToString() ?? "Day";

        var groups = viewMode switch
        {
            "Day" => GroupByDay(),
            "Teacher" => GroupByTeacher(),
            "Studio" => GroupByStudio(),
            "Student" => GroupByStudent(),
            _ => GroupByDay()
        };

        BuildScheduleUI(groups);
    }

    private void BuildScheduleUI(List<ScheduleGroup> groups)
    {
        ScheduleContainer.Children.Clear();

        foreach (var group in groups)
        {
            var groupBorder = new Border
            {
                BackgroundColor = Color.FromArgb("#FAFAFA"),
                Stroke = Color.FromArgb("#E0E0E0"),
                StrokeThickness = 1,
                Padding = new Thickness(10)
            };

            var groupStack = new VerticalStackLayout { Spacing = 10 };

            // Group Header
            groupStack.Children.Add(new Label
            {
                Text = group.GroupHeader,
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
                TextColor = Color.FromArgb("#2563EB")
            });

            // SubGroups
            foreach (var subGroup in group.SubGroups)
            {
                var subGroupStack = new VerticalStackLayout { Spacing = 5, Margin = new Thickness(10, 0, 0, 0) };

                // SubGroup Header
                if (!string.IsNullOrEmpty(subGroup.SubGroupHeader))
                {
                    subGroupStack.Children.Add(new Label
                    {
                        Text = subGroup.SubGroupHeader,
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 13,
                        TextColor = Color.FromArgb("#4A5568")
                    });
                }

                // Classes
                foreach (var cls in subGroup.Classes)
                {
                    var classBorder = new Border
                    {
                        BackgroundColor = Colors.White,
                        Stroke = Color.FromArgb("#DDDDDD"),
                        StrokeThickness = 1,
                        StrokeShape = new RoundRectangle { CornerRadius = 3 },
                        Padding = new Thickness(8),
                        Margin = new Thickness(0, 2)
                    };

                    var classGrid = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = new GridLength(90) },
                            new ColumnDefinition { Width = GridLength.Star },
                            new ColumnDefinition { Width = GridLength.Auto },
                            new ColumnDefinition { Width = GridLength.Auto }
                        },
                        ColumnSpacing = 10
                    };

                    // Time
                    classGrid.Add(new Label
                    {
                        Text = cls.TimeRange,
                        FontAttributes = FontAttributes.Bold,
                        VerticalOptions = LayoutOptions.Center
                    }, 0);

                    // Class Name & Students
                    var nameStack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
                    nameStack.Children.Add(new Label
                    {
                        Text = cls.ClassName,
                        FontAttributes = FontAttributes.Bold
                    });
                    nameStack.Children.Add(new Label
                    {
                        Text = cls.StudentNames,
                        TextColor = Color.FromArgb("#666666"),
                        FontSize = 11,
                        LineBreakMode = LineBreakMode.TailTruncation
                    });
                    classGrid.Add(nameStack, 1);

                    // Teacher
                    if (!string.IsNullOrEmpty(cls.TeacherName))
                    {
                        classGrid.Add(new Label
                        {
                            Text = cls.TeacherName,
                            TextColor = Color.FromArgb("#555555"),
                            VerticalOptions = LayoutOptions.Center
                        }, 2);
                    }

                    // Studio
                    if (!string.IsNullOrEmpty(cls.StudioName))
                    {
                        classGrid.Add(new Label
                        {
                            Text = cls.StudioName,
                            TextColor = Color.FromArgb("#555555"),
                            VerticalOptions = LayoutOptions.Center,
                            MinimumWidthRequest = 100
                        }, 3);
                    }

                    classBorder.Content = classGrid;
                    subGroupStack.Children.Add(classBorder);
                }

                groupStack.Children.Add(subGroupStack);
            }

            groupBorder.Content = groupStack;
            ScheduleContainer.Children.Add(groupBorder);
        }
    }

    private static readonly DayOfWeek[] DayOrder = 
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    private List<ScheduleGroup> GroupByDay()
    {
        return _scheduledClasses
            .GroupBy(c => c.Day)
            .OrderBy(g => Array.IndexOf(DayOrder, g.Key))
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
        return _scheduledClasses
            .GroupBy(c => c.TeacherId)
            .OrderBy(g => GetTeacherName(g.Key))
            .Select(teacherGroup => new ScheduleGroup
            {
                GroupHeader = GetTeacherName(teacherGroup.Key),
                SubGroups = teacherGroup
                    .GroupBy(c => c.Day)
                    .OrderBy(g => Array.IndexOf(DayOrder, g.Key))
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
        return _scheduledClasses
            .GroupBy(c => c.StudioId)
            .OrderBy(g => GetStudioName(g.Key))
            .Select(studioGroup => new ScheduleGroup
            {
                GroupHeader = GetStudioName(studioGroup.Key),
                SubGroups = studioGroup
                    .GroupBy(c => c.Day)
                    .OrderBy(g => Array.IndexOf(DayOrder, g.Key))
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
        var selectedItem = StudentFilterPicker.SelectedItem as StudentItem;
        var selectedStudentId = selectedItem?.Id;

        var filteredClasses = selectedStudentId.HasValue
            ? _scheduledClasses.Where(c => c.StudentIds.Contains(selectedStudentId.Value)).ToList()
            : _scheduledClasses;

        if (selectedStudentId.HasValue)
        {
            // Single student view - group by day only
            return filteredClasses
                .GroupBy(c => c.Day)
                .OrderBy(g => Array.IndexOf(DayOrder, g.Key))
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
                        .OrderBy(g => Array.IndexOf(DayOrder, g.Key))
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

    private static ClassViewModel CreateClassViewModel(ScheduledClass c, bool showTeacher = true, bool showStudio = true)
    {
        return new ClassViewModel
        {
            TimeRange = $"{c.StartTime:HH\\:mm}-{c.EndTime:HH\\:mm}",
            ClassName = GetClassName(c),
            StudentNames = GetStudentNames(c),
            TeacherName = showTeacher ? GetTeacherName(c.TeacherId) : null,
            StudioName = showStudio ? GetStudioName(c.StudioId) : null
        };
    }

    private static string GetClassName(ScheduledClass c)
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

    private static string GetStudentNames(ScheduledClass c)
    {
        var names = c.StudentIds
            .Select(id => AppData.Current.Students.FirstOrDefault(s => s.Id == id)?.Name ?? "Unknown")
            .OrderBy(n => n)
            .ToList();

        if (names.Count == 0) return "";
        if (names.Count <= 3) return string.Join(", ", names);
        return $"{string.Join(", ", names.Take(3))} +{names.Count - 3} more";
    }

    private static string GetTeacherName(Guid teacherId)
    {
        return AppData.Current.Teachers.FirstOrDefault(t => t.Id == teacherId)?.Name ?? "Unknown";
    }

    private static string GetStudioName(Guid studioId)
    {
        return AppData.Current.Studios.FirstOrDefault(s => s.Id == studioId)?.Name ?? "Unknown";
    }

    private static string GetStudentName(Guid studentId)
    {
        return AppData.Current.Students.FirstOrDefault(s => s.Id == studentId)?.Name ?? "Unknown";
    }
}

public class StudentItem
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
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

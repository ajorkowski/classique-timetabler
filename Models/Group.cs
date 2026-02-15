using System.ComponentModel;
using System.Text.Json.Serialization;
using classique.timetabler.Data;

namespace classique.timetabler.Models
{
    public class Group : INotifyPropertyChanged
    {
        private Guid _id = Guid.NewGuid();
        private string _name = string.Empty;
        private Guid? _studioId;
        private List<Guid> _teacherIds = new();
        private bool _isFixedTime;
        private DayOfWeek _day = DayOfWeek.Monday;
        private TimeOnly _startTime = new TimeOnly(9, 0);
        private TimeOnly _endTime = new TimeOnly(10, 0);
        private int _durationMinutes = 20;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Guid Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public Guid? StudioId
        {
            get => _studioId;
            set 
            { 
                _studioId = value; 
                OnPropertyChanged(nameof(StudioId)); 
                OnPropertyChanged(nameof(StudioName));
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(ValidationWarnings));
                OnPropertyChanged(nameof(WarningsSummary));
            }
        }

        public List<Guid> TeacherIds
        {
            get => _teacherIds;
            set 
            { 
                _teacherIds = value; 
                OnPropertyChanged(nameof(TeacherIds)); 
                OnPropertyChanged(nameof(TeacherNames)); 
                OnPropertyChanged(nameof(FirstTeacherName));
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(ValidationWarnings));
                OnPropertyChanged(nameof(WarningsSummary));
            }
        }

        /// <summary>
        /// If true, the group has a fixed time slot. If false, the scheduler can move it around.
        /// </summary>
        public bool IsFixedTime
        {
            get => _isFixedTime;
            set 
            { 
                _isFixedTime = value; 
                OnPropertyChanged(nameof(IsFixedTime));
                OnPropertyChanged(nameof(ScheduleDisplay));
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(DayGrouping));
                OnPropertyChanged(nameof(SortableStartTime));
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(ValidationWarnings));
                OnPropertyChanged(nameof(WarningsSummary));
            }
        }

        public DayOfWeek Day
        {
            get => _day;
            set { _day = value; OnPropertyChanged(nameof(Day)); OnPropertyChanged(nameof(ScheduleDisplay)); OnPropertyChanged(nameof(DayGrouping)); }
        }

        public TimeOnly StartTime
        {
            get => _startTime;
            set 
            { 
                _startTime = value; 
                OnPropertyChanged(nameof(StartTime)); 
                OnPropertyChanged(nameof(ScheduleDisplay)); 
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(SortableStartTime));
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(ValidationWarnings));
                OnPropertyChanged(nameof(WarningsSummary));
            }
        }

        public TimeOnly EndTime
        {
            get => _endTime;
            set 
            { 
                _endTime = value; 
                OnPropertyChanged(nameof(EndTime)); 
                OnPropertyChanged(nameof(ScheduleDisplay)); 
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(ValidationWarnings));
                OnPropertyChanged(nameof(WarningsSummary));
            }
        }

        /// <summary>
        /// Duration in minutes. For flexible groups, this is set manually.
        /// For fixed time groups, this is calculated from start/end times.
        /// </summary>
        public int DurationMinutes
        {
            get => _durationMinutes;
            set 
            { 
                _durationMinutes = value; 
                OnPropertyChanged(nameof(DurationMinutes)); 
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(ScheduleDisplay));
            }
        }

        /// <summary>
        /// Gets the duration as a TimeSpan. For fixed time groups, calculated from start/end.
        /// For flexible groups, uses the DurationMinutes property.
        /// </summary>
        [JsonIgnore]
        public TimeSpan Duration => _isFixedTime 
            ? _endTime - _startTime 
            : TimeSpan.FromMinutes(_durationMinutes);

        // Computed properties for display
        [JsonIgnore]
        public string StudioName => _studioId.HasValue 
            ? AppData.Current.Studios.FirstOrDefault(s => s.Id == _studioId)?.Name ?? "Unknown"
            : "Not assigned";

        [JsonIgnore]
        public string TeacherNames
        {
            get
            {
                if (_teacherIds.Count == 0) return "Not assigned";
                var names = _teacherIds
                    .Select(id => AppData.Current.Teachers.FirstOrDefault(t => t.Id == id)?.Name)
                    .Where(n => n != null)
                    .ToList();
                return names.Count > 0 ? string.Join(", ", names) : "Not assigned";
            }
        }

        /// <summary>
        /// First teacher name for sorting purposes
        /// </summary>
        [JsonIgnore]
        public string FirstTeacherName
        {
            get
            {
                if (_teacherIds.Count == 0) return "zzz"; // Sort to end
                var firstTeacherId = _teacherIds.First();
                var teacher = AppData.Current.Teachers.FirstOrDefault(t => t.Id == firstTeacherId);
                return teacher?.Name ?? "zzz";
            }
        }

        /// <summary>
        /// Day grouping - returns "Flexible" for flexible groups, day name for fixed
        /// </summary>
        [JsonIgnore]
        public string DayGrouping => _isFixedTime ? _day.ToString() : "Flexible";

        /// <summary>
        /// Sortable start time - flexible groups get max time to sort to end
        /// </summary>
        [JsonIgnore]
        public TimeOnly SortableStartTime => _isFixedTime ? _startTime : TimeOnly.MaxValue;

        [JsonIgnore]
        public string ScheduleDisplay => _isFixedTime 
            ? $"{_day} {_startTime:h:mm tt} - {_endTime:h:mm tt}"
            : $"Flexible ({_durationMinutes} min)";

        /// <summary>
        /// Number of students enrolled in this group
        /// </summary>
        [JsonIgnore]
        public int StudentCount => AppData.Current.Students.Count(s => s.GroupIds.Contains(_id));

        /// <summary>
        /// List of student names enrolled in this group
        /// </summary>
        [JsonIgnore]
        public List<string> StudentNames => AppData.Current.Students
            .Where(s => s.GroupIds.Contains(_id))
            .Select(s => s.Name)
            .OrderBy(n => n)
            .ToList();

        /// <summary>
        /// Comma-separated student names for display
        /// </summary>
        [JsonIgnore]
        public string StudentNamesDisplay => StudentCount == 0 ? "No students" : string.Join(", ", StudentNames);

        /// <summary>
        /// Returns true if there are validation warnings
        /// </summary>
        [JsonIgnore]
        public bool HasWarnings => ValidationWarnings.Count > 0;

        /// <summary>
        /// List of validation warnings for this group
        /// </summary>
        [JsonIgnore]
        public List<string> ValidationWarnings
        {
            get
            {
                var warnings = new List<string>();

                // All groups need at least one teacher
                if (_teacherIds.Count == 0)
                {
                    warnings.Add("No teacher assigned");
                }

                // Flexible groups can only have one teacher
                if (!_isFixedTime && _teacherIds.Count > 1)
                {
                    warnings.Add("Flexible groups can only have one teacher assigned");
                }

                // Fixed groups need a studio
                if (_isFixedTime && !_studioId.HasValue)
                {
                    warnings.Add("No studio assigned");
                }

                // Fixed groups: end time must be after start time
                if (_isFixedTime && _endTime <= _startTime)
                {
                    warnings.Add("End time must be after start time");
                }

                return warnings;
            }
        }

        /// <summary>
        /// Single string summary of warnings for display
        /// </summary>
        [JsonIgnore]
        public string WarningsSummary => HasWarnings ? string.Join("; ", ValidationWarnings) : string.Empty;

        public void NotifyTeacherChanged()
        {
            OnPropertyChanged(nameof(TeacherNames));
            OnPropertyChanged(nameof(FirstTeacherName));
            OnPropertyChanged(nameof(HasWarnings));
            OnPropertyChanged(nameof(ValidationWarnings));
            OnPropertyChanged(nameof(WarningsSummary));
        }

        public void NotifyStudentsChanged()
        {
            OnPropertyChanged(nameof(StudentCount));
            OnPropertyChanged(nameof(StudentNames));
            OnPropertyChanged(nameof(StudentNamesDisplay));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

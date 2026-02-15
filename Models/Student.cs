using System.ComponentModel;
using System.Text.Json.Serialization;
using classique.timetabler.Data;

namespace classique.timetabler.Models
{
    public class StudentSolo : INotifyPropertyChanged
    {
        private Guid _id = Guid.NewGuid();
        private string _name = string.Empty;
        private int _durationMinutes = 10;
        private Guid? _teacherId;

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

        public int DurationMinutes
        {
            get => _durationMinutes;
            set { _durationMinutes = value; OnPropertyChanged(nameof(DurationMinutes)); }
        }

        public Guid? TeacherId
        {
            get => _teacherId;
            set { _teacherId = value; OnPropertyChanged(nameof(TeacherId)); OnPropertyChanged(nameof(TeacherName)); }
        }

        [JsonIgnore]
        public string TeacherName => _teacherId.HasValue
            ? AppData.Current.Teachers.FirstOrDefault(t => t.Id == _teacherId)?.Name ?? "Unknown"
            : "Not assigned";

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class StudentUnavailability : INotifyPropertyChanged
    {
        private DayOfWeek _day;
        private TimeOnly _startTime;
        private TimeOnly _endTime;

        public event PropertyChangedEventHandler? PropertyChanged;

        public DayOfWeek Day
        {
            get => _day;
            set { _day = value; OnPropertyChanged(nameof(Day)); }
        }

        public TimeOnly StartTime
        {
            get => _startTime;
            set { _startTime = value; OnPropertyChanged(nameof(StartTime)); }
        }

        public TimeOnly EndTime
        {
            get => _endTime;
            set { _endTime = value; OnPropertyChanged(nameof(EndTime)); }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Student : INotifyPropertyChanged
    {
        private Guid _id = Guid.NewGuid();
        private string _name = string.Empty;
        private int _yearOfBirth = DateTime.Now.Year - 10;
        private List<Guid> _groupIds = new();
        private List<StudentSolo> _solos = new();
        private List<StudentUnavailability> _unavailability = new();

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

        public int YearOfBirth
        {
            get => _yearOfBirth;
            set { _yearOfBirth = value; OnPropertyChanged(nameof(YearOfBirth)); OnPropertyChanged(nameof(Age)); }
        }

        public List<Guid> GroupIds
        {
            get => _groupIds;
            set { _groupIds = value; OnPropertyChanged(nameof(GroupIds)); OnPropertyChanged(nameof(GroupNames)); }
        }

        public List<StudentSolo> Solos
        {
            get => _solos;
            set { _solos = value; OnPropertyChanged(nameof(Solos)); }
        }

        public List<StudentUnavailability> Unavailability
        {
            get => _unavailability;
            set { _unavailability = value; OnPropertyChanged(nameof(Unavailability)); }
        }

        [JsonIgnore]
        public int Age => DateTime.Now.Year - _yearOfBirth;

        [JsonIgnore]
        public string GroupNames
        {
            get
            {
                if (_groupIds.Count == 0) return "None";
                var names = _groupIds
                    .Select(id => AppData.Current.Groups.FirstOrDefault(g => g.Id == id)?.Name)
                    .Where(n => n != null)
                    .ToList();
                return names.Count > 0 ? string.Join(", ", names) : "None";
            }
        }

        [JsonIgnore]
        public string SolosSummary => _solos.Count == 0 ? "None" : $"{_solos.Count} solo(s)";

        public void NotifyGroupsChanged()
        {
            OnPropertyChanged(nameof(GroupNames));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

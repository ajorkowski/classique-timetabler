using System.ComponentModel;
using System.Text.Json.Serialization;
using ClassiqueTimetabler.Maui.Data;

namespace ClassiqueTimetabler.Maui.Models;

public class TeacherAvailability : INotifyPropertyChanged
{
    private Guid _studioId;
    private DayOfWeek _day;
    private TimeOnly _startTime;
    private TimeOnly _endTime;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid StudioId
    {
        get => _studioId;
        set { _studioId = value; OnPropertyChanged(nameof(StudioId)); OnPropertyChanged(nameof(StudioName)); }
    }

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

    [JsonIgnore]
    public string StudioName => AppData.Current.Studios.FirstOrDefault(s => s.Id == _studioId)?.Name ?? "Unknown";

    [JsonIgnore]
    public TimeSpan Duration => EndTime - StartTime;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class Teacher : INotifyPropertyChanged
{
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private List<TeacherAvailability> _availability = new();

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

    public List<TeacherAvailability> Availability
    {
        get => _availability;
        set { _availability = value; OnPropertyChanged(nameof(Availability)); NotifyValidationChanged(); }
    }

    [JsonIgnore]
    public TimeSpan TotalAvailability
    {
        get
        {
            var total = TimeSpan.Zero;
            foreach (var slot in _availability)
            {
                if (slot.EndTime > slot.StartTime)
                {
                    total += slot.Duration;
                }
            }
            return total;
        }
    }

    [JsonIgnore]
    public int GroupCount => AppData.Current.Groups.Count(g => g.TeacherIds.Contains(_id));

    [JsonIgnore]
    public TimeSpan GroupDuration
    {
        get
        {
            var total = TimeSpan.Zero;
            foreach (var group in AppData.Current.Groups.Where(g => g.TeacherIds.Contains(_id)))
            {
                total += group.Duration;
            }
            return total;
        }
    }

    [JsonIgnore]
    public int SoloCount
    {
        get
        {
            int count = 0;
            foreach (var student in AppData.Current.Students)
            {
                count += student.Solos.Count(s => s.TeacherId == _id);
            }
            return count;
        }
    }

    [JsonIgnore]
    public TimeSpan SoloDuration
    {
        get
        {
            var total = TimeSpan.Zero;
            foreach (var student in AppData.Current.Students)
            {
                foreach (var solo in student.Solos.Where(s => s.TeacherId == _id))
                {
                    total += TimeSpan.FromMinutes(solo.DurationMinutes);
                }
            }
            return total;
        }
    }

    [JsonIgnore]
    public TimeSpan TotalWorkload => GroupDuration + SoloDuration;

    [JsonIgnore]
    public string WorkloadSummary =>
        $"Groups: {GroupCount} ({FormatDuration(GroupDuration)}) | Solos: {SoloCount} ({FormatDuration(SoloDuration)})";

    [JsonIgnore]
    public bool IsValid => ValidationWarnings.Count == 0;

    [JsonIgnore]
    public bool HasWarnings => ValidationWarnings.Count > 0;

    [JsonIgnore]
    public List<string> ValidationWarnings
    {
        get
        {
            var warnings = new List<string>();

            if (_availability.Count == 0)
            {
                warnings.Add("No availability set");
            }

            foreach (var slot in _availability)
            {
                if (slot.EndTime <= slot.StartTime)
                {
                    warnings.Add($"Invalid time range on {slot.Day}: end time must be after start time");
                }

                if (slot.StudioId == Guid.Empty || !AppData.Current.Studios.Any(s => s.Id == slot.StudioId))
                {
                    warnings.Add($"Invalid or missing studio on {slot.Day}");
                }
            }

            for (int i = 0; i < _availability.Count; i++)
            {
                for (int j = i + 1; j < _availability.Count; j++)
                {
                    var slotA = _availability[i];
                    var slotB = _availability[j];

                    if (slotA.StudioId == slotB.StudioId &&
                        slotA.Day == slotB.Day &&
                        TimesOverlap(slotA, slotB))
                    {
                        var studioName = slotA.StudioName;
                        warnings.Add($"Overlapping times at {studioName} on {slotA.Day}");
                    }
                }
            }

            foreach (var slot in _availability)
            {
                foreach (var otherTeacher in AppData.Current.Teachers.Where(t => t.Id != _id))
                {
                    foreach (var otherSlot in otherTeacher.Availability)
                    {
                        if (slot.StudioId == otherSlot.StudioId &&
                            slot.Day == otherSlot.Day &&
                            TimesOverlap(slot, otherSlot))
                        {
                            var studioName = slot.StudioName;
                            warnings.Add($"Studio conflict with {otherTeacher.Name} at {studioName} on {slot.Day} ({slot.StartTime:h:mm tt}-{slot.EndTime:h:mm tt})");
                        }
                    }
                }
            }

            if (_availability.Count > 0 && TotalWorkload > TotalAvailability)
            {
                warnings.Add($"Workload ({FormatDuration(TotalWorkload)}) exceeds availability ({FormatDuration(TotalAvailability)})");
            }

            return warnings;
        }
    }

    [JsonIgnore]
    public string WarningsSummary => HasWarnings ? string.Join("; ", ValidationWarnings) : string.Empty;

    private bool TimesOverlap(TeacherAvailability a, TeacherAvailability b)
    {
        return a.StartTime < b.EndTime && b.StartTime < a.EndTime;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }
        return $"{(int)duration.TotalMinutes}m";
    }

    public void NotifyValidationChanged()
    {
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(ValidationWarnings));
        OnPropertyChanged(nameof(WarningsSummary));
        OnPropertyChanged(nameof(TotalAvailability));
        OnPropertyChanged(nameof(GroupCount));
        OnPropertyChanged(nameof(GroupDuration));
        OnPropertyChanged(nameof(SoloCount));
        OnPropertyChanged(nameof(SoloDuration));
        OnPropertyChanged(nameof(TotalWorkload));
        OnPropertyChanged(nameof(WorkloadSummary));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

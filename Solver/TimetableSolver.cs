using Google.OrTools.Sat;
using classique.timetabler.Data;
using classique.timetabler.Models;

namespace classique.timetabler.Solver
{
    /// <summary>
    /// Represents a scheduling alternative for a class (teacher + day + time window)
    /// </summary>
    internal class ClassAlternative
    {
        public Guid TeacherId { get; set; }
        public Guid StudioId { get; set; }
        public DayOfWeek Day { get; set; }
        public int WindowStartMinutes { get; set; }
        public int WindowEndMinutes { get; set; }
    }

    /// <summary>
    /// Internal representation of a class to be scheduled
    /// </summary>
    internal class ClassToSchedule
    {
        public Guid Id { get; set; }
        public bool IsSolo { get; set; }
        public Guid? SoloId { get; set; }
        public Guid? StudentId { get; set; }
        public Guid? GroupId { get; set; }
        public int DurationMinutes { get; set; }
        public List<Guid> StudentIds { get; set; } = new();
        public int MinStudentAge { get; set; } = 18;
        public List<ClassAlternative> Alternatives { get; set; } = new();
    }

    /// <summary>
    /// Solver for the dance studio timetable scheduling problem.
    /// Uses Google OR-Tools CP-SAT solver.
    /// </summary>
    public class TimetableSolver
    {
        private readonly TimetableData _data;
        private readonly int _timeLimitSeconds;

        // Minutes from midnight for time calculations
        private const int DayStartMinutes = 0;
        private const int DayEndMinutes = 24 * 60;

        public TimetableSolver(TimetableData data, int timeLimitSeconds = 60)
        {
            _data = data;
            _timeLimitSeconds = timeLimitSeconds;
        }

        public ScheduleResult Solve()
        {
            var result = new ScheduleResult
            {
                AlphaMakespan = _data.AlphaMakespan,
                BetaStudentClustering = _data.BetaStudentClustering,
                GammaAgePriority = _data.GammaAgePriority,
                CrossDayPenalty = _data.CrossDayPenalty
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Build the list of classes to schedule (flexible groups and solos)
                var classesToSchedule = BuildClassesToSchedule();

                if (classesToSchedule.Count == 0)
                {
                    result.IsFeasible = true;
                    result.IsOptimal = true;
                    result.SolverMessage = "No flexible classes to schedule.";
                    result.SolveTime = stopwatch.Elapsed;
                    
                    // Add fixed groups to result
                    AddFixedGroupsToResult(result);
                    return result;
                }

                // Check if all classes have at least one alternative
                var classesWithoutAlternatives = classesToSchedule.Where(c => c.Alternatives.Count == 0).ToList();
                if (classesWithoutAlternatives.Count > 0)
                {
                    result.IsFeasible = false;
                    result.SolverMessage = $"No valid scheduling options for {classesWithoutAlternatives.Count} class(es). Check teacher availability.";
                    result.SolveTime = stopwatch.Elapsed;
                    return result;
                }

                // Create the CP-SAT model
                var model = new CpModel();

                // Create variables
                var classVars = CreateClassVariables(model, classesToSchedule);

                // Add constraints
                AddAlternativeSelectionConstraints(model, classesToSchedule, classVars);
                AddTeacherNoOverlapConstraints(model, classesToSchedule, classVars);
                AddStudentNoOverlapConstraints(model, classesToSchedule, classVars);

                // Create objective
                var objectiveTerms = new List<LinearExpr>();

                // Makespan objective
                if (_data.AlphaMakespan > 0)
                {
                    var makespan = model.NewIntVar(0, DayEndMinutes, "makespan");
                    foreach (var classId in classVars.Keys)
                    {
                        foreach (var altVar in classVars[classId].AlternativeVars)
                        {
                            // makespan >= end when alternative is selected
                            model.Add(makespan >= altVar.End).OnlyEnforceIf(altVar.IsPresent);
                        }
                    }
                    objectiveTerms.Add((long)(_data.AlphaMakespan * 100) * makespan);
                }

                // Age penalty objective (younger students earlier)
                if (_data.GammaAgePriority > 0)
                {
                    foreach (var cls in classesToSchedule)
                    {
                        if (cls.MinStudentAge > 0)
                        {
                            // Penalty inversely proportional to age
                            var ageFactor = (long)(100.0 / cls.MinStudentAge);
                            foreach (var altVar in classVars[cls.Id].AlternativeVars)
                            {
                                var penalty = model.NewIntVar(0, DayEndMinutes * ageFactor, $"age_penalty_{cls.Id}_{altVar.Day}");
                                model.Add(penalty == ageFactor * altVar.Start).OnlyEnforceIf(altVar.IsPresent);
                                model.Add(penalty == 0).OnlyEnforceIf(altVar.IsPresent.Not());
                                objectiveTerms.Add((long)(_data.GammaAgePriority) * penalty);
                            }
                        }
                    }
                }

                if (objectiveTerms.Count > 0)
                {
                    model.Minimize(LinearExpr.Sum(objectiveTerms));
                }

                // Solve
                var solver = new CpSolver();
                solver.StringParameters = $"max_time_in_seconds:{_timeLimitSeconds}";

                var status = solver.Solve(model);

                stopwatch.Stop();
                result.SolveTime = stopwatch.Elapsed;

                if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
                {
                    result.IsFeasible = true;
                    result.IsOptimal = status == CpSolverStatus.Optimal;
                    result.ObjectiveValue = solver.ObjectiveValue;

                    // Extract solution
                    ExtractSolution(solver, classesToSchedule, classVars, result);
                    
                    // Add fixed groups to result
                    AddFixedGroupsToResult(result);

                    result.SolverMessage = status == CpSolverStatus.Optimal
                        ? "Optimal solution found."
                        : "Feasible solution found (may not be optimal).";
                }
                else
                {
                    result.IsFeasible = false;
                    result.SolverMessage = status switch
                    {
                        CpSolverStatus.Infeasible => "No feasible solution exists. Check constraints.",
                        CpSolverStatus.ModelInvalid => "Model is invalid.",
                        _ => $"Solver returned status: {status}"
                    };
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.SolveTime = stopwatch.Elapsed;
                result.IsFeasible = false;
                result.SolverMessage = $"Solver error: {ex.Message}";
            }

            return result;
        }

        private List<ClassToSchedule> BuildClassesToSchedule()
        {
            var classes = new List<ClassToSchedule>();

            // Get teacher availability windows, excluding fixed group times
            var teacherWindows = BuildTeacherAvailabilityWindows();

            // Add flexible groups
            foreach (var group in _data.Groups.Where(g => !g.IsFixedTime))
            {
                if (group.TeacherIds.Count == 0) continue;

                var cls = new ClassToSchedule
                {
                    Id = Guid.NewGuid(),
                    IsSolo = false,
                    GroupId = group.Id,
                    DurationMinutes = group.DurationMinutes,
                    StudentIds = _data.Students
                        .Where(s => s.GroupIds.Contains(group.Id))
                        .Select(s => s.Id)
                        .ToList()
                };

                // Calculate min student age
                var studentAges = _data.Students
                    .Where(s => s.GroupIds.Contains(group.Id))
                    .Select(s => s.Age)
                    .ToList();
                cls.MinStudentAge = studentAges.Count > 0 ? studentAges.Min() : 18;

                // Generate alternatives from teacher availability
                foreach (var teacherId in group.TeacherIds)
                {
                    if (teacherWindows.TryGetValue(teacherId, out var windows))
                    {
                        foreach (var window in windows)
                        {
                            if (window.EndMinutes - window.StartMinutes >= cls.DurationMinutes)
                            {
                                cls.Alternatives.Add(new ClassAlternative
                                {
                                    TeacherId = teacherId,
                                    StudioId = window.StudioId,
                                    Day = window.Day,
                                    WindowStartMinutes = window.StartMinutes,
                                    WindowEndMinutes = window.EndMinutes
                                });
                            }
                        }
                    }
                }

                if (cls.Alternatives.Count > 0)
                {
                    classes.Add(cls);
                }
            }

            // Add solos
            foreach (var student in _data.Students)
            {
                foreach (var solo in student.Solos)
                {
                    if (!solo.TeacherId.HasValue) continue;

                    var cls = new ClassToSchedule
                    {
                        Id = Guid.NewGuid(),
                        IsSolo = true,
                        SoloId = solo.Id,
                        StudentId = student.Id,
                        GroupId = null,
                        DurationMinutes = solo.DurationMinutes,
                        StudentIds = new List<Guid> { student.Id },
                        MinStudentAge = student.Age
                    };

                    // Generate alternatives from teacher availability
                    if (teacherWindows.TryGetValue(solo.TeacherId.Value, out var windows))
                    {
                        foreach (var window in windows)
                        {
                            if (window.EndMinutes - window.StartMinutes >= cls.DurationMinutes)
                            {
                                cls.Alternatives.Add(new ClassAlternative
                                {
                                    TeacherId = solo.TeacherId.Value,
                                    StudioId = window.StudioId,
                                    Day = window.Day,
                                    WindowStartMinutes = window.StartMinutes,
                                    WindowEndMinutes = window.EndMinutes
                                });
                            }
                        }
                    }

                    if (cls.Alternatives.Count > 0)
                    {
                        classes.Add(cls);
                    }
                }
            }

            return classes;
        }

        private class AvailabilityWindow
        {
            public Guid StudioId { get; set; }
            public DayOfWeek Day { get; set; }
            public int StartMinutes { get; set; }
            public int EndMinutes { get; set; }
        }

        private Dictionary<Guid, List<AvailabilityWindow>> BuildTeacherAvailabilityWindows()
        {
            var result = new Dictionary<Guid, List<AvailabilityWindow>>();

            // Get fixed groups by teacher
            var fixedGroupsByTeacher = _data.Groups
                .Where(g => g.IsFixedTime && g.TeacherIds.Count > 0)
                .SelectMany(g => g.TeacherIds.Select(t => new { TeacherId = t, Group = g }))
                .GroupBy(x => x.TeacherId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Group).ToList());

            foreach (var teacher in _data.Teachers)
            {
                var windows = new List<AvailabilityWindow>();

                foreach (var availability in teacher.Availability)
                {
                    // Skip if no valid studio
                    if (availability.StudioId == Guid.Empty) continue;

                    var dayWindows = new List<AvailabilityWindow>
                    {
                        new AvailabilityWindow
                        {
                            StudioId = availability.StudioId,
                            Day = availability.Day,
                            StartMinutes = availability.StartTime.Hour * 60 + availability.StartTime.Minute,
                            EndMinutes = availability.EndTime.Hour * 60 + availability.EndTime.Minute
                        }
                    };

                    // Subtract fixed group times
                    if (fixedGroupsByTeacher.TryGetValue(teacher.Id, out var fixedGroups))
                    {
                        foreach (var fixedGroup in fixedGroups.Where(g => g.Day == availability.Day))
                        {
                            var fixedStart = fixedGroup.StartTime.Hour * 60 + fixedGroup.StartTime.Minute;
                            var fixedEnd = fixedGroup.EndTime.Hour * 60 + fixedGroup.EndTime.Minute;

                            dayWindows = SplitWindows(dayWindows, fixedStart, fixedEnd);
                        }
                    }

                    windows.AddRange(dayWindows);
                }

                if (windows.Count > 0)
                {
                    result[teacher.Id] = windows;
                }
            }

            return result;
        }

        private List<AvailabilityWindow> SplitWindows(List<AvailabilityWindow> windows, int excludeStart, int excludeEnd)
        {
            var result = new List<AvailabilityWindow>();

            foreach (var window in windows)
            {
                if (excludeEnd <= window.StartMinutes || excludeStart >= window.EndMinutes)
                {
                    // No overlap
                    result.Add(window);
                }
                else
                {
                    // Split the window
                    if (excludeStart > window.StartMinutes)
                    {
                        result.Add(new AvailabilityWindow
                        {
                            StudioId = window.StudioId,
                            Day = window.Day,
                            StartMinutes = window.StartMinutes,
                            EndMinutes = excludeStart
                        });
                    }
                    if (excludeEnd < window.EndMinutes)
                    {
                        result.Add(new AvailabilityWindow
                        {
                            StudioId = window.StudioId,
                            Day = window.Day,
                            StartMinutes = excludeEnd,
                            EndMinutes = window.EndMinutes
                        });
                    }
                }
            }

            return result;
        }

        private class AlternativeVariable
        {
            public ClassAlternative Alternative { get; set; } = null!;
            public IntVar Start { get; set; } = null!;
            public IntVar End { get; set; } = null!;
            public IntervalVar Interval { get; set; } = null!;
            public ILiteral IsPresent { get; set; } = null!;
            public DayOfWeek Day => Alternative.Day;
        }

        private class ClassVariables
        {
            public List<AlternativeVariable> AlternativeVars { get; set; } = new();
        }

        private Dictionary<Guid, ClassVariables> CreateClassVariables(CpModel model, List<ClassToSchedule> classes)
        {
            var result = new Dictionary<Guid, ClassVariables>();

            foreach (var cls in classes)
            {
                var classVars = new ClassVariables();

                for (int i = 0; i < cls.Alternatives.Count; i++)
                {
                    var alt = cls.Alternatives[i];
                    var suffix = $"{cls.Id}_{i}";

                    var isPresent = model.NewBoolVar($"present_{suffix}");
                    var start = model.NewIntVar(alt.WindowStartMinutes, alt.WindowEndMinutes - cls.DurationMinutes, $"start_{suffix}");
                    var end = model.NewIntVar(alt.WindowStartMinutes + cls.DurationMinutes, alt.WindowEndMinutes, $"end_{suffix}");
                    var interval = model.NewOptionalIntervalVar(start, cls.DurationMinutes, end, isPresent, $"interval_{suffix}");

                    classVars.AlternativeVars.Add(new AlternativeVariable
                    {
                        Alternative = alt,
                        Start = start,
                        End = end,
                        Interval = interval,
                        IsPresent = isPresent
                    });
                }

                result[cls.Id] = classVars;
            }

            return result;
        }

        private void AddAlternativeSelectionConstraints(CpModel model, List<ClassToSchedule> classes, Dictionary<Guid, ClassVariables> classVars)
        {
            foreach (var cls in classes)
            {
                // Exactly one alternative must be selected
                var presenceVars = classVars[cls.Id].AlternativeVars.Select(a => a.IsPresent).ToList();
                model.AddExactlyOne(presenceVars);
            }
        }

        private void AddTeacherNoOverlapConstraints(CpModel model, List<ClassToSchedule> classes, Dictionary<Guid, ClassVariables> classVars)
        {
            // Group intervals by (teacher, day)
            var intervalsByTeacherDay = new Dictionary<(Guid, DayOfWeek), List<IntervalVar>>();

            foreach (var cls in classes)
            {
                foreach (var altVar in classVars[cls.Id].AlternativeVars)
                {
                    var key = (altVar.Alternative.TeacherId, altVar.Alternative.Day);
                    if (!intervalsByTeacherDay.ContainsKey(key))
                    {
                        intervalsByTeacherDay[key] = new List<IntervalVar>();
                    }
                    intervalsByTeacherDay[key].Add(altVar.Interval);
                }
            }

            // Add NoOverlap constraint for each (teacher, day)
            foreach (var kvp in intervalsByTeacherDay)
            {
                if (kvp.Value.Count > 1)
                {
                    model.AddNoOverlap(kvp.Value);
                }
            }
        }

        private void AddStudentNoOverlapConstraints(CpModel model, List<ClassToSchedule> classes, Dictionary<Guid, ClassVariables> classVars)
        {
            // Group classes by student
            var classesByStudent = new Dictionary<Guid, List<ClassToSchedule>>();
            foreach (var cls in classes)
            {
                foreach (var studentId in cls.StudentIds)
                {
                    if (!classesByStudent.ContainsKey(studentId))
                    {
                        classesByStudent[studentId] = new List<ClassToSchedule>();
                    }
                    classesByStudent[studentId].Add(cls);
                }
            }

            // For each student, group intervals by day and add NoOverlap
            foreach (var kvp in classesByStudent)
            {
                var studentClasses = kvp.Value;
                if (studentClasses.Count <= 1) continue;

                var intervalsByDay = new Dictionary<DayOfWeek, List<IntervalVar>>();

                foreach (var cls in studentClasses)
                {
                    foreach (var altVar in classVars[cls.Id].AlternativeVars)
                    {
                        var day = altVar.Alternative.Day;
                        if (!intervalsByDay.ContainsKey(day))
                        {
                            intervalsByDay[day] = new List<IntervalVar>();
                        }
                        intervalsByDay[day].Add(altVar.Interval);
                    }
                }

                foreach (var dayIntervals in intervalsByDay.Values)
                {
                    if (dayIntervals.Count > 1)
                    {
                        model.AddNoOverlap(dayIntervals);
                    }
                }
            }
        }

        private void ExtractSolution(CpSolver solver, List<ClassToSchedule> classes, Dictionary<Guid, ClassVariables> classVars, ScheduleResult result)
        {
            foreach (var cls in classes)
            {
                foreach (var altVar in classVars[cls.Id].AlternativeVars)
                {
                    if (solver.BooleanValue(altVar.IsPresent))
                    {
                        var startMinutes = (int)solver.Value(altVar.Start);
                        var endMinutes = (int)solver.Value(altVar.End);

                        result.ScheduledClasses.Add(new ScheduledClass
                        {
                            Id = Guid.NewGuid(),
                            IsSolo = cls.IsSolo,
                            SoloId = cls.SoloId,
                            StudentId = cls.StudentId,
                            GroupId = cls.GroupId,
                            TeacherId = altVar.Alternative.TeacherId,
                            StudioId = altVar.Alternative.StudioId,
                            Day = altVar.Alternative.Day,
                            StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(startMinutes)),
                            EndTime = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(endMinutes)),
                            DurationMinutes = cls.DurationMinutes
                        });

                        // Track makespan
                        if (endMinutes > result.MakespanMinutes)
                        {
                            result.MakespanMinutes = endMinutes;
                        }

                        break;
                    }
                }
            }
        }

        private void AddFixedGroupsToResult(ScheduleResult result)
        {
            foreach (var group in _data.Groups.Where(g => g.IsFixedTime))
            {
                if (group.TeacherIds.Count == 0 || !group.StudioId.HasValue) continue;

                result.ScheduledClasses.Add(new ScheduledClass
                {
                    Id = Guid.NewGuid(),
                    IsSolo = false,
                    GroupId = group.Id,
                    TeacherId = group.TeacherIds.First(),
                    StudioId = group.StudioId.Value,
                    Day = group.Day,
                    StartTime = group.StartTime,
                    EndTime = group.EndTime,
                    DurationMinutes = (int)(group.EndTime - group.StartTime).TotalMinutes
                });
            }
        }
    }
}
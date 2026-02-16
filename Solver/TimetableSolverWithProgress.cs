using ClassiqueTimetabler.Maui.Data;
using ClassiqueTimetabler.Maui.Models;
using Google.OrTools.Sat;

namespace ClassiqueTimetabler.Maui.Solver;

/// <summary>
/// Solver with progress reporting for the UI.
/// Wraps the core solving logic with logging capabilities.
/// </summary>
public class TimetableSolverWithProgress
{
    private readonly TimetableData _data;
    private readonly int _timeLimitSeconds;
    private readonly SolverProgress _progress;

    private const int DayEndMinutes = 24 * 60;

    public TimetableSolverWithProgress(TimetableData data, int timeLimitSeconds, SolverProgress progress)
    {
        _data = data;
        _timeLimitSeconds = timeLimitSeconds;
        _progress = progress;
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
            _progress.SetStatus("Analyzing input data...");
            _progress.Log($"Input Data Summary:");
            _progress.Log($"  Teachers: {_data.Teachers.Count}");
            _progress.Log($"  Studios: {_data.Studios.Count}");
            _progress.Log($"  Students: {_data.Students.Count}");
            _progress.Log($"  Groups: {_data.Groups.Count}");

            var fixedGroups = _data.Groups.Count(g => g.IsFixedTime);
            var flexibleGroups = _data.Groups.Count - fixedGroups;
            _progress.Log($"    - Fixed: {fixedGroups}");
            _progress.Log($"    - Flexible: {flexibleGroups}");

            var totalSolos = _data.Students.Sum(s => s.Solos.Count);
            _progress.Log($"  Solos: {totalSolos}");
            _progress.Log("");

            // Build the list of classes to schedule
            _progress.SetStatus("Building scheduling alternatives...");
            var classesToSchedule = BuildClassesToSchedule();
            _progress.SetClassCount(classesToSchedule.Count);

            var totalAlternatives = classesToSchedule.Sum(c => c.Alternatives.Count);
            _progress.SetAlternativesCount(totalAlternatives);

            _progress.Log($"Classes to schedule: {classesToSchedule.Count}");
            _progress.Log($"  Flexible groups: {classesToSchedule.Count(c => !c.IsSolo)}");
            _progress.Log($"  Solos: {classesToSchedule.Count(c => c.IsSolo)}");
            _progress.Log($"Total scheduling alternatives: {totalAlternatives:N0}");
            _progress.Log("");

            if (classesToSchedule.Count == 0)
            {
                result.IsFeasible = true;
                result.IsOptimal = true;
                result.SolverMessage = "No flexible classes to schedule.";
                result.SolveTime = stopwatch.Elapsed;
                AddFixedGroupsToResult(result);
                _progress.Log("No flexible classes to schedule - using fixed groups only.");
                return result;
            }

            // Check for classes without alternatives
            var classesWithoutAlternatives = classesToSchedule.Where(c => c.Alternatives.Count == 0).ToList();
            if (classesWithoutAlternatives.Count > 0)
            {
                result.IsFeasible = false;
                result.SolverMessage = $"No valid scheduling options for {classesWithoutAlternatives.Count} class(es). Check teacher availability.";
                result.SolveTime = stopwatch.Elapsed;
                _progress.Log($"ERROR: {classesWithoutAlternatives.Count} class(es) have no valid scheduling options.");
                return result;
            }

            // Create the CP-SAT model
            _progress.SetStatus("Creating constraint model...");
            _progress.Log("Creating CP-SAT constraint model...");
            var model = new CpModel();

            // Create variables
            _progress.Log("  Creating decision variables...");
            var classVars = CreateClassVariables(model, classesToSchedule);

            int totalVars = classesToSchedule.Sum(c => classVars[c.Id].AlternativeVars.Count * 4);
            _progress.SetVariablesCount(totalVars);
            _progress.Log($"    Created {totalVars:N0} variables");

            // Add constraints
            _progress.Log("  Adding constraints...");

            _progress.Log("    - Alternative selection constraints");
            AddAlternativeSelectionConstraints(model, classesToSchedule, classVars);

            _progress.Log("    - Teacher no-overlap constraints");
            int teacherConstraints = AddTeacherNoOverlapConstraints(model, classesToSchedule, classVars);
            _progress.Log($"      ({teacherConstraints} constraint groups)");

            _progress.Log("    - Student no-overlap constraints");
            int studentConstraints = AddStudentNoOverlapConstraints(model, classesToSchedule, classVars);
            _progress.Log($"      ({studentConstraints} constraint groups)");

            _progress.Log("    - Student unavailability constraints");
            int unavailConstraints = AddStudentUnavailabilityConstraints(model, classesToSchedule, classVars);
            _progress.Log($"      ({unavailConstraints} constraints)");

            // Create objective
            _progress.Log("  Building objective function...");
            var objectiveTerms = new List<LinearExpr>();

            if (_data.AlphaMakespan > 0)
            {
                _progress.Log($"    - Free time objective (alpha={_data.AlphaMakespan})");
                AddFreeTimeObjective(model, classesToSchedule, classVars, objectiveTerms);
            }

            if (_data.BetaStudentClustering > 0)
            {
                _progress.Log($"    - Student clustering objective (beta={_data.BetaStudentClustering})");
                AddStudentClusteringObjective(model, classesToSchedule, classVars, objectiveTerms);
            }

            if (_data.GammaAgePriority > 0)
            {
                _progress.Log($"    - Age priority objective (gamma={_data.GammaAgePriority})");
                AddAgePriorityObjective(model, classesToSchedule, classVars, objectiveTerms);
            }

            _progress.Log($"    Total objective terms: {objectiveTerms.Count:N0}");

            if (objectiveTerms.Count > 0)
            {
                model.Minimize(LinearExpr.Sum(objectiveTerms));
            }

            _progress.Log("");

            // Solve
            _progress.SetStatus("Solving... (this may take a while)");
            _progress.Log($"Starting solver with {_timeLimitSeconds}s time limit...");
            _progress.Log("");

            var solver = new CpSolver();
            solver.StringParameters = $"max_time_in_seconds:{_timeLimitSeconds}";

            var status = solver.Solve(model);

            stopwatch.Stop();
            result.SolveTime = stopwatch.Elapsed;

            _progress.Log($"Solver completed in {result.SolveTime.TotalSeconds:F2} seconds");
            _progress.Log($"Status: {status}");

            if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
            {
                result.IsFeasible = true;
                result.IsOptimal = status == CpSolverStatus.Optimal;
                result.ObjectiveValue = (long)solver.ObjectiveValue;

                ExtractSolution(solver, classesToSchedule, classVars, result);
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
            _progress.Log($"ERROR: {ex.Message}");
        }

        return result;
    }

    private List<ClassToSchedule> BuildClassesToSchedule()
    {
        var classes = new List<ClassToSchedule>();
        var teacherWindows = BuildTeacherAvailabilityWindows();

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

            var studentAges = _data.Students
                .Where(s => s.GroupIds.Contains(group.Id))
                .Select(s => s.Age)
                .ToList();
            cls.MinStudentAge = studentAges.Count > 0 ? studentAges.Min() : 18;

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
                                WindowEndMinutes = window.EndMinutes,
                                TeacherDayStartMinutes = window.TeacherDayStartMinutes
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
                    GroupId = null,
                    DurationMinutes = solo.DurationMinutes,
                    StudentIds = [student.Id],
                    MinStudentAge = student.Age
                };

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
                                WindowEndMinutes = window.EndMinutes,
                                TeacherDayStartMinutes = window.TeacherDayStartMinutes
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

    private Dictionary<Guid, List<AvailabilityWindow>> BuildTeacherAvailabilityWindows()
    {
        var result = new Dictionary<Guid, List<AvailabilityWindow>>();

        var fixedGroupsByTeacher = _data.Groups
            .Where(g => g.IsFixedTime && g.TeacherIds.Count > 0)
            .SelectMany(g => g.TeacherIds.Select(t => new { TeacherId = t, Group = g }))
            .GroupBy(x => x.TeacherId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Group).ToList());

        foreach (var teacher in _data.Teachers)
        {
            var windows = new List<AvailabilityWindow>();

            var availabilityByDay = teacher.Availability
                .Where(a => a.StudioId != Guid.Empty)
                .GroupBy(a => a.Day);

            foreach (var dayGroup in availabilityByDay)
            {
                var day = dayGroup.Key;
                var dayStartMinutes = dayGroup.Min(a => a.StartTime.Hour * 60 + a.StartTime.Minute);

                foreach (var availability in dayGroup)
                {
                    var dayWindows = new List<AvailabilityWindow>
                    {
                        new()
                        {
                            StudioId = availability.StudioId,
                            Day = availability.Day,
                            StartMinutes = availability.StartTime.Hour * 60 + availability.StartTime.Minute,
                            EndMinutes = availability.EndTime.Hour * 60 + availability.EndTime.Minute,
                            TeacherDayStartMinutes = dayStartMinutes
                        }
                    };

                    if (fixedGroupsByTeacher.TryGetValue(teacher.Id, out var fixedGroups))
                    {
                        foreach (var fixedGroup in fixedGroups.Where(g => g.Day == availability.Day))
                        {
                            var fixedStart = fixedGroup.StartTime.Hour * 60 + fixedGroup.StartTime.Minute;
                            var fixedEnd = fixedGroup.EndTime.Hour * 60 + fixedGroup.EndTime.Minute;
                            dayWindows = SplitWindows(dayWindows, fixedStart, fixedEnd, dayStartMinutes);
                        }
                    }

                    windows.AddRange(dayWindows);
                }
            }

            if (windows.Count > 0)
            {
                result[teacher.Id] = windows;
            }
        }

        return result;
    }

    private static List<AvailabilityWindow> SplitWindows(List<AvailabilityWindow> windows, int excludeStart, int excludeEnd, int teacherDayStartMinutes)
    {
        var result = new List<AvailabilityWindow>();

        foreach (var window in windows)
        {
            if (excludeEnd <= window.StartMinutes || excludeStart >= window.EndMinutes)
            {
                result.Add(window);
            }
            else
            {
                if (excludeStart > window.StartMinutes)
                {
                    result.Add(new AvailabilityWindow
                    {
                        StudioId = window.StudioId,
                        Day = window.Day,
                        StartMinutes = window.StartMinutes,
                        EndMinutes = excludeStart,
                        TeacherDayStartMinutes = teacherDayStartMinutes
                    });
                }
                if (excludeEnd < window.EndMinutes)
                {
                    result.Add(new AvailabilityWindow
                    {
                        StudioId = window.StudioId,
                        Day = window.Day,
                        StartMinutes = excludeEnd,
                        EndMinutes = window.EndMinutes,
                        TeacherDayStartMinutes = teacherDayStartMinutes
                    });
                }
            }
        }

        return result;
    }

    private static Dictionary<Guid, ClassVariables> CreateClassVariables(CpModel model, List<ClassToSchedule> classes)
    {
        var result = new Dictionary<Guid, ClassVariables>();

        foreach (var cls in classes)
        {
            var classVars = new ClassVariables();

            for (int i = 0; i < cls.Alternatives.Count; i++)
            {
                var alt = cls.Alternatives[i];
                var suffix = $"{cls.Id}_{i}";
                var windowDuration = alt.WindowEndMinutes - alt.WindowStartMinutes;

                var isPresent = model.NewBoolVar($"present_{suffix}");
                var start = model.NewIntVar(0, windowDuration - cls.DurationMinutes, $"start_{suffix}");
                var end = model.NewIntVar(cls.DurationMinutes, windowDuration, $"end_{suffix}");
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

    private static void AddAlternativeSelectionConstraints(CpModel model, List<ClassToSchedule> classes, Dictionary<Guid, ClassVariables> classVars)
    {
        foreach (var cls in classes)
        {
            var presenceVars = classVars[cls.Id].AlternativeVars.Select(a => a.IsPresent).ToList();
            model.AddExactlyOne(presenceVars);
        }
    }

    private static int AddTeacherNoOverlapConstraints(CpModel model, List<ClassToSchedule> classes, Dictionary<Guid, ClassVariables> classVars)
    {
        var intervalsByTeacherDayWindow = new Dictionary<(Guid, DayOfWeek, int, int), List<IntervalVar>>();

        foreach (var cls in classes)
        {
            foreach (var altVar in classVars[cls.Id].AlternativeVars)
            {
                var key = (altVar.Alternative.TeacherId, altVar.Alternative.Day,
                           altVar.Alternative.WindowStartMinutes, altVar.Alternative.WindowEndMinutes);
                if (!intervalsByTeacherDayWindow.ContainsKey(key))
                {
                    intervalsByTeacherDayWindow[key] = [];
                }
                intervalsByTeacherDayWindow[key].Add(altVar.Interval);
            }
        }

        int constraintCount = 0;
        foreach (var kvp in intervalsByTeacherDayWindow)
        {
            if (kvp.Value.Count > 1)
            {
                model.AddNoOverlap(kvp.Value);
                constraintCount++;
            }
        }

        return constraintCount;
    }

    private static int AddStudentNoOverlapConstraints(CpModel model, List<ClassToSchedule> classes, Dictionary<Guid, ClassVariables> classVars)
    {
        var classesByStudent = new Dictionary<Guid, List<ClassToSchedule>>();
        foreach (var cls in classes)
        {
            foreach (var studentId in cls.StudentIds)
            {
                if (!classesByStudent.ContainsKey(studentId))
                {
                    classesByStudent[studentId] = [];
                }
                classesByStudent[studentId].Add(cls);
            }
        }

        int constraintCount = 0;
        foreach (var kvp in classesByStudent)
        {
            var studentClasses = kvp.Value;
            if (studentClasses.Count <= 1) continue;

            var intervalsByDayWindow = new Dictionary<(DayOfWeek, int, int), List<IntervalVar>>();

            foreach (var cls in studentClasses)
            {
                foreach (var altVar in classVars[cls.Id].AlternativeVars)
                {
                    var key = (altVar.Alternative.Day, altVar.Alternative.WindowStartMinutes, altVar.Alternative.WindowEndMinutes);
                    if (!intervalsByDayWindow.ContainsKey(key))
                    {
                        intervalsByDayWindow[key] = [];
                    }
                    intervalsByDayWindow[key].Add(altVar.Interval);
                }
            }

            foreach (var dayWindowIntervals in intervalsByDayWindow.Values)
            {
                if (dayWindowIntervals.Count > 1)
                {
                    model.AddNoOverlap(dayWindowIntervals);
                    constraintCount++;
                }
            }
        }

        return constraintCount;
    }

    private int AddStudentUnavailabilityConstraints(CpModel model, List<ClassToSchedule> classes, Dictionary<Guid, ClassVariables> classVars)
    {
        int constraintCount = 0;

        foreach (var cls in classes)
        {
            foreach (var altVar in classVars[cls.Id].AlternativeVars)
            {
                var alt = altVar.Alternative;

                foreach (var studentId in cls.StudentIds)
                {
                    var student = _data.Students.FirstOrDefault(s => s.Id == studentId);
                    if (student == null) continue;

                    foreach (var unavail in student.Unavailability)
                    {
                        if (unavail.Day != alt.Day) continue;

                        var unavailStart = unavail.StartTime.Hour * 60 + unavail.StartTime.Minute;
                        var unavailEnd = unavail.EndTime.Hour * 60 + unavail.EndTime.Minute;

                        if (unavailEnd <= alt.WindowStartMinutes || unavailStart >= alt.WindowEndMinutes)
                        {
                            continue;
                        }

                        var relativeUnavailStart = Math.Max(0, unavailStart - alt.WindowStartMinutes);
                        var relativeUnavailEnd = Math.Min(alt.WindowEndMinutes - alt.WindowStartMinutes, unavailEnd - alt.WindowStartMinutes);

                        var isBefore = model.NewBoolVar($"before_unavail_{cls.Id}_{alt.Day}_{unavailStart}_{studentId}");

                        model.Add(altVar.Start + cls.DurationMinutes <= relativeUnavailStart)
                            .OnlyEnforceIf([altVar.IsPresent, isBefore]);

                        model.Add(altVar.Start >= relativeUnavailEnd)
                            .OnlyEnforceIf([altVar.IsPresent, isBefore.Not()]);

                        constraintCount++;
                    }
                }
            }
        }

        return constraintCount;
    }

    private void ExtractSolution(CpSolver solver, List<ClassToSchedule> classes, Dictionary<Guid, ClassVariables> classVars, ScheduleResult result)
    {
        foreach (var cls in classes)
        {
            foreach (var altVar in classVars[cls.Id].AlternativeVars)
            {
                if (solver.BooleanValue(altVar.IsPresent))
                {
                    var relativeStart = (int)solver.Value(altVar.Start);
                    var relativeEnd = (int)solver.Value(altVar.End);
                    var absoluteStart = altVar.Alternative.WindowStartMinutes + relativeStart;
                    var absoluteEnd = altVar.Alternative.WindowStartMinutes + relativeEnd;

                    result.ScheduledClasses.Add(new ScheduledClass
                    {
                        Id = Guid.NewGuid(),
                        IsSolo = cls.IsSolo,
                        SoloId = cls.SoloId,
                        StudentIds = cls.StudentIds,
                        GroupId = cls.GroupId,
                        TeacherId = altVar.Alternative.TeacherId,
                        StudioId = altVar.Alternative.StudioId,
                        Day = altVar.Alternative.Day,
                        StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(absoluteStart)),
                        EndTime = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(absoluteEnd)),
                        DurationMinutes = cls.DurationMinutes
                    });

                    if (absoluteEnd > result.MakespanMinutes)
                    {
                        result.MakespanMinutes = absoluteEnd;
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

            var studentIds = _data.Students
                .Where(s => s.GroupIds.Contains(group.Id))
                .Select(s => s.Id)
                .ToList();

            result.ScheduledClasses.Add(new ScheduledClass
            {
                Id = Guid.NewGuid(),
                IsSolo = false,
                GroupId = group.Id,
                StudentIds = studentIds,
                TeacherId = group.TeacherIds.First(),
                StudioId = group.StudioId.Value,
                Day = group.Day,
                StartTime = group.StartTime,
                EndTime = group.EndTime,
                DurationMinutes = (int)(group.EndTime - group.StartTime).TotalMinutes
            });
        }
    }

    #region Objective Functions

    private void AddFreeTimeObjective(CpModel model, List<ClassToSchedule> classes, Dictionary<Guid, ClassVariables> classVars, List<LinearExpr> objectiveTerms)
    {
        if (_data.AlphaMakespan <= 0) return;

        foreach (var cls in classes)
        {
            foreach (var altVar in classVars[cls.Id].AlternativeVars)
            {
                var windowOffset = altVar.Alternative.WindowStartMinutes - altVar.Alternative.TeacherDayStartMinutes;
                var maxPenalty = DayEndMinutes - altVar.Alternative.TeacherDayStartMinutes;
                var endPenalty = model.NewIntVar(0, maxPenalty, $"end_penalty_{cls.Id}_{altVar.Day}");

                model.Add(endPenalty == windowOffset + altVar.End).OnlyEnforceIf(altVar.IsPresent);
                model.Add(endPenalty == 0).OnlyEnforceIf(altVar.IsPresent.Not());

                objectiveTerms.Add(_data.AlphaMakespan * endPenalty);
            }
        }
    }

    private void AddStudentClusteringObjective(CpModel model, List<ClassToSchedule> classes, Dictionary<Guid, ClassVariables> classVars, List<LinearExpr> objectiveTerms)
    {
        if (_data.BetaStudentClustering <= 0) return;

        var fixedGroupsByStudent = new Dictionary<Guid, List<FixedClassInfo>>();
        foreach (var group in _data.Groups.Where(g => g.IsFixedTime))
        {
            var studentsInGroup = _data.Students.Where(s => s.GroupIds.Contains(group.Id)).Select(s => s.Id);
            foreach (var studentId in studentsInGroup)
            {
                if (!fixedGroupsByStudent.ContainsKey(studentId))
                {
                    fixedGroupsByStudent[studentId] = [];
                }
                fixedGroupsByStudent[studentId].Add(new FixedClassInfo
                {
                    Day = group.Day,
                    StartMinutes = group.StartTime.Hour * 60 + group.StartTime.Minute,
                    EndMinutes = group.EndTime.Hour * 60 + group.EndTime.Minute
                });
            }
        }

        var classesByStudent = new Dictionary<Guid, List<ClassToSchedule>>();
        foreach (var cls in classes)
        {
            foreach (var studentId in cls.StudentIds)
            {
                if (!classesByStudent.ContainsKey(studentId))
                {
                    classesByStudent[studentId] = [];
                }
                classesByStudent[studentId].Add(cls);
            }
        }

        foreach (var studentId in classesByStudent.Keys)
        {
            var studentFlexibleClasses = classesByStudent[studentId];
            var studentFixedGroups = fixedGroupsByStudent.GetValueOrDefault(studentId, []);

            if (studentFlexibleClasses.Count + studentFixedGroups.Count <= 1) continue;

            for (int i = 0; i < studentFlexibleClasses.Count; i++)
            {
                for (int j = i + 1; j < studentFlexibleClasses.Count; j++)
                {
                    var class1 = studentFlexibleClasses[i];
                    var class2 = studentFlexibleClasses[j];

                    foreach (var alt1 in classVars[class1.Id].AlternativeVars)
                    {
                        foreach (var alt2 in classVars[class2.Id].AlternativeVars)
                        {
                            AddFlexibleFlexibleGapPenalty(model, class1, class2, alt1, alt2, objectiveTerms);
                        }
                    }
                }
            }

            foreach (var flexClass in studentFlexibleClasses)
            {
                foreach (var fixedGroup in studentFixedGroups)
                {
                    foreach (var alt in classVars[flexClass.Id].AlternativeVars)
                    {
                        AddFlexibleFixedGapPenalty(model, flexClass, alt, fixedGroup, objectiveTerms);
                    }
                }
            }
        }
    }

    private void AddFlexibleFlexibleGapPenalty(CpModel model, ClassToSchedule class1, ClassToSchedule class2,
        AlternativeVariable alt1, AlternativeVariable alt2, List<LinearExpr> objectiveTerms)
    {
        var bothPresent = new[] { alt1.IsPresent, alt2.IsPresent };

        if (alt1.Day == alt2.Day)
        {
            var maxGap = DayEndMinutes;
            var gap = model.NewIntVar(0, maxGap, $"gap_{class1.Id}_{class2.Id}_{alt1.Day}");

            var gap1 = model.NewIntVar(-maxGap, maxGap, $"gap1_{class1.Id}_{class2.Id}");
            var gap2 = model.NewIntVar(-maxGap, maxGap, $"gap2_{class1.Id}_{class2.Id}");

            model.Add(gap1 >= alt2.Alternative.WindowStartMinutes + alt2.Start - alt1.Alternative.WindowStartMinutes - alt1.End).OnlyEnforceIf(bothPresent);
            model.Add(gap2 >= alt1.Alternative.WindowStartMinutes + alt1.Start - alt2.Alternative.WindowStartMinutes - alt2.End).OnlyEnforceIf(bothPresent);
            model.AddMaxEquality(gap, [gap1, gap2]);
            model.Add(gap == 0).OnlyEnforceIf(alt1.IsPresent.Not());
            model.Add(gap == 0).OnlyEnforceIf(alt2.IsPresent.Not());

            objectiveTerms.Add(_data.BetaStudentClustering * gap);
        }
        else
        {
            var crossDayPenalty = model.NewIntVar(0, _data.CrossDayPenalty, $"cross_{class1.Id}_{class2.Id}_{alt1.Day}_{alt2.Day}");
            model.Add(crossDayPenalty == _data.CrossDayPenalty).OnlyEnforceIf(bothPresent);
            model.Add(crossDayPenalty == 0).OnlyEnforceIf(alt1.IsPresent.Not());
            model.Add(crossDayPenalty == 0).OnlyEnforceIf(alt2.IsPresent.Not());

            objectiveTerms.Add(_data.BetaStudentClustering * crossDayPenalty);
        }
    }

    private void AddFlexibleFixedGapPenalty(CpModel model, ClassToSchedule flexClass, AlternativeVariable alt,
        FixedClassInfo fixedGroup, List<LinearExpr> objectiveTerms)
    {
        if (alt.Day == fixedGroup.Day)
        {
            var maxGap = DayEndMinutes;
            var gap = model.NewIntVar(0, maxGap, $"gap_fixed_{flexClass.Id}_{fixedGroup.StartMinutes}_{alt.Day}");

            var gap1 = model.NewIntVar(-maxGap, maxGap, $"gap1_fixed_{flexClass.Id}_{fixedGroup.StartMinutes}");
            var gap2 = model.NewIntVar(-maxGap, maxGap, $"gap2_fixed_{flexClass.Id}_{fixedGroup.StartMinutes}");

            model.Add(gap1 == fixedGroup.StartMinutes - alt.Alternative.WindowStartMinutes - alt.End).OnlyEnforceIf(alt.IsPresent);
            model.Add(gap2 == alt.Alternative.WindowStartMinutes + alt.Start - fixedGroup.EndMinutes).OnlyEnforceIf(alt.IsPresent);
            model.AddMaxEquality(gap, [gap1, gap2]);
            model.Add(gap == 0).OnlyEnforceIf(alt.IsPresent.Not());

            objectiveTerms.Add(_data.BetaStudentClustering * gap);
        }
        else
        {
            var crossDayPenalty = model.NewIntVar(0, _data.CrossDayPenalty, $"cross_fixed_{flexClass.Id}_{fixedGroup.StartMinutes}_{alt.Day}");
            model.Add(crossDayPenalty == _data.CrossDayPenalty).OnlyEnforceIf(alt.IsPresent);
            model.Add(crossDayPenalty == 0).OnlyEnforceIf(alt.IsPresent.Not());

            objectiveTerms.Add(_data.BetaStudentClustering * crossDayPenalty);
        }
    }

    private void AddAgePriorityObjective(CpModel model, List<ClassToSchedule> classes, Dictionary<Guid, ClassVariables> classVars, List<LinearExpr> objectiveTerms)
    {
        if (_data.GammaAgePriority <= 0) return;

        foreach (var cls in classes)
        {
            if (cls.MinStudentAge <= 0) continue;

            var ageFactor = 100L / cls.MinStudentAge;

            foreach (var altVar in classVars[cls.Id].AlternativeVars)
            {
                var windowOffset = altVar.Alternative.WindowStartMinutes - altVar.Alternative.TeacherDayStartMinutes;
                var maxPenalty = (DayEndMinutes - altVar.Alternative.TeacherDayStartMinutes) * ageFactor;
                var penalty = model.NewIntVar(0, maxPenalty, $"age_penalty_{cls.Id}_{altVar.Day}");

                model.Add(penalty == ageFactor * (windowOffset + altVar.Start)).OnlyEnforceIf(altVar.IsPresent);
                model.Add(penalty == 0).OnlyEnforceIf(altVar.IsPresent.Not());

                objectiveTerms.Add(_data.GammaAgePriority * penalty);
            }
        }
    }

    #endregion

    #region Private Classes

    private class ClassAlternative
    {
        public Guid TeacherId { get; set; }
        public Guid StudioId { get; set; }
        public DayOfWeek Day { get; set; }
        public int WindowStartMinutes { get; set; }
        public int WindowEndMinutes { get; set; }
        public int TeacherDayStartMinutes { get; set; }
    }

    private class ClassToSchedule
    {
        public Guid Id { get; set; }
        public bool IsSolo { get; set; }
        public Guid? SoloId { get; set; }
        public Guid? GroupId { get; set; }
        public int DurationMinutes { get; set; }
        public List<Guid> StudentIds { get; set; } = [];
        public int MinStudentAge { get; set; } = 18;
        public List<ClassAlternative> Alternatives { get; set; } = [];
    }

    private class AvailabilityWindow
    {
        public Guid StudioId { get; set; }
        public DayOfWeek Day { get; set; }
        public int StartMinutes { get; set; }
        public int EndMinutes { get; set; }
        public int TeacherDayStartMinutes { get; set; }
    }

    private class FixedClassInfo
    {
        public DayOfWeek Day { get; set; }
        public int StartMinutes { get; set; }
        public int EndMinutes { get; set; }
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
        public List<AlternativeVariable> AlternativeVars { get; set; } = [];
    }

    #endregion
}

public enum ProgressUpdateType
{
    Log,
    Status,
    ClassCount,
    AlternativesCount,
    VariablesCount
}

public class SolverProgressUpdate
{
    public ProgressUpdateType Type { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Progress reporter for the solver that works with MAUI's IProgress
/// </summary>
public class SolverProgress
{
    private readonly IProgress<SolverProgressUpdate>? _progress;

    public SolverProgress(IProgress<SolverProgressUpdate>? progress = null)
    {
        _progress = progress;
    }

    public void Log(string message)
    {
        _progress?.Report(new SolverProgressUpdate { Type = ProgressUpdateType.Log, Message = message });
    }

    public void SetStatus(string status)
    {
        _progress?.Report(new SolverProgressUpdate { Type = ProgressUpdateType.Status, Message = status });
    }

    public void SetClassCount(int count)
    {
        _progress?.Report(new SolverProgressUpdate { Type = ProgressUpdateType.ClassCount, Message = count.ToString() });
    }

    public void SetAlternativesCount(int count)
    {
        _progress?.Report(new SolverProgressUpdate { Type = ProgressUpdateType.AlternativesCount, Message = count.ToString("N0") });
    }

    public void SetVariablesCount(int count)
    {
        _progress?.Report(new SolverProgressUpdate { Type = ProgressUpdateType.VariablesCount, Message = count.ToString("N0") });
    }
}

# Classique Timetabler

A WPF application designed to help create optimized timetables for a dance studio using constraint programming.

## Overview

This program assists dance studios in scheduling classes by considering teachers, studios, students, and class types to generate an optimal timetable. It uses the Google OR-Tools CP-SAT solver to find schedules that minimize teaching time while clustering student activities and prioritizing younger students for earlier time slots.

## Core Concepts

### Entities

- **Teachers**: Instructors who teach dance classes, each with defined availability windows
- **Studios**: Physical rooms/spaces where classes are held (coupled with teacher availability)
- **Students**: Individuals enrolled in dance classes
  - Each student has an **age** which is used for scheduling optimization (younger students get earlier slots)
  - Students can have **unavailability windows** for times they cannot attend
- **Groups**: Dance classes with multiple students
  - **Fixed Groups**: Scheduled at a specific day/time (pre-allocated)
  - **Flexible Groups**: The solver determines when to schedule them
- **Solos**: Individual student performances/lessons assigned to a specific teacher

### Relationships

- Each student can be enrolled in multiple groups and have multiple solos
- Flexible groups are assigned to a single teacher; fixed groups can have multiple teachers
- Teachers are coupled with studios during their availability windows

## Scheduling Problem

The timetable scheduling is modeled as a **constraint satisfaction and optimization problem**, similar to the flexible job shop scheduling problem.

### Constraints

1. **Task Duration**: Each class has a fixed duration
2. **Alternative Selection**: Each flexible class must be assigned to exactly one time slot
3. **Teacher Availability**: Classes must fit within teacher availability windows
4. **No Teacher Overlap**: A teacher cannot teach two classes simultaneously
5. **No Student Overlap**: A student cannot attend two classes at the same time
6. **Student Availability**: Classes cannot be scheduled during student unavailability windows
7. **Makespan**: Track the latest ending time across all classes

### Optimization Objectives

The solver minimizes a weighted combination of:

- **Alpha (Makespan)**: Prioritize finishing all classes as early as possible
- **Beta (Student Clustering)**: Minimize gaps between a student's classes (encourages same-day scheduling)
- **Gamma (Age Priority)**: Schedule younger students' classes earlier in the day
- **W_cross (Cross-Day Penalty)**: High penalty for scheduling a student's classes on different days

### Pre-processing

Before solving:
1. Fixed groups are identified and their time slots are subtracted from teacher availability
2. This may split continuous availability windows into smaller segments
3. Scheduling alternatives are generated for each flexible class based on available windows

## Application Structure

The application uses a tabbed interface:

1. **Studios Tab**: Configure studio information
2. **Teachers Tab**: Configure teacher information and availability (linked to studios)
3. **Groups Tab**: Define group classes (fixed or flexible)
4. **Students Tab**: Manage student information, group enrollments, solos, and unavailability
5. **Generate Tab**: Configure optimization weights and generate the timetable

## Save and Load

The application supports saving and loading your work:

- **Auto-save**: Periodically saves your work to prevent data loss
- **Save/Save As**: Export all configured data to a `.timetable` file
- **Load**: Import previously saved configurations
- **Continue**: Resume from the last auto-saved session

### File Format (.timetable)

The `.timetable` file format is a compressed JSON file:

1. **Data Structure**: All application data is stored in a single root class containing:
   - Teachers, Studios, Students, Groups collections
   - Optimization weights (Alpha, Beta, Gamma, W_cross)
   - Generated scheduled classes

2. **Format**: JSON compressed with ZIP for compact file sizes

## Technology

- **Framework**: WPF (.NET 10)
- **Language**: C# 14
- **Solver**: [Google OR-Tools CP-SAT](https://developers.google.com/optimization/cp/cp_solver)

## Documentation

See [docs/CONSTRAINT_PROBLEM.md](docs/CONSTRAINT_PROBLEM.md) for a detailed mathematical formulation of the scheduling problem.

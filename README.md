# Classique Timetabler

A WPF application designed to help create optimized timetables for a dance studio.

## Overview

This program assists dance studios in scheduling classes by considering teachers, studios, students, and class types to generate an optimal timetable that minimizes student wait times.

## Core Concepts

### Entities

- **Teachers**: Instructors who teach dance classes
- **Studios**: Physical rooms/spaces where classes are held
- **Students**: Individuals enrolled in dance classes
  - Each student has an **age** which is used for scheduling optimization
- **Classes**: Dance sessions with the following properties:
  - **Name**: The name of the class
  - **Linked Teachers**: One or more teachers assigned to the class (at least one required)
  - **Linked Studio**: The studio where the class takes place (required)
  - **Day**: The date of the class
  - **Start Time**: When the class begins
  - **End Time**: When the class ends
  - **IsBlock**: Determines the class type:
    - `false` = Fixed class - a regular scheduled lesson at a specific time
    - `true` = Block - a time block where multiple students can be booked consecutively (one after the other)

### Relationships

- Each student can be enrolled in multiple group classes and solos
- Each class is taught by a teacher in a specific studio
- Teachers and studios have availability constraints

## Goals

The timetabler aims to:

1. Schedule all classes so that students can attend all their enrolled groups and solos
2. Minimize waiting time between classes for each student
3. Avoid scheduling conflicts for teachers and studios
4. Schedule younger students earlier in the day
5. Optimize the overall timetable for the best possible experience

## Application Structure

The application uses a tabbed interface with the following sections:

1. **Teachers Tab**: Configure teacher information and availability
2. **Studios Tab**: Configure studio information and availability
3. **Classes Tab**: Define group classes and solos
4. **Students Tab**: Manage student information and enrollments
5. **Generate Tab**: Generate and view the optimized timetable

## Save and Load

The application supports saving and loading your work:

- **Save**: Export all configured data (teachers, studios, students, classes, and generated timetables) to a file
- **Load**: Import previously saved configurations to continue working or make adjustments

This allows users to:
- Save work in progress and continue later
- Create multiple timetable variations for comparison
- Back up configurations before making major changes
- Share configurations between different installations

### File Format (.timetable)

The `.timetable` file format is a compressed JSON file:

1. **Data Structure**: All application data is stored in a single root class that contains:
   - Teachers collection
   - Studios collection
   - Students collection
   - Classes collection (groups and solos)
   - Generated timetable data

2. **Serialization Process**:
   - The root data class is serialized to JSON
   - The JSON is then compressed using ZIP compression
   - The resulting file is saved with the `.timetable` extension

3. **Deserialization Process**:
   - The `.timetable` file is decompressed
   - The JSON content is deserialized back into the root data class
   - The application state is restored from the loaded data

This format provides:
- **Compact file sizes** through ZIP compression
- **Human-readable data** (when unzipped) for debugging
- **Single-file storage** for easy sharing and backup

## Technology

- **Framework**: WPF (.NET 10)
- **Language**: C#

namespace ClassiqueTimetabler.Maui.Data;

public static class AppData
{
    public static TimetableData Current { get; set; } = new();

    public static void Reset()
    {
        Current = new TimetableData();
    }
}

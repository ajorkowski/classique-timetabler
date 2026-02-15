namespace classique.timetabler.Models
{
    public class Studio
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
    }
}

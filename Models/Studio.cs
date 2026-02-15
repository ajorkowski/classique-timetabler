using System.ComponentModel;

namespace classique.timetabler.Models
{
    public class Studio : INotifyPropertyChanged
    {
        private Guid _id = Guid.NewGuid();
        private string _name = string.Empty;

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

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

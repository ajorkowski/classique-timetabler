using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using classique.timetabler.Data;
using classique.timetabler.Models;

namespace classique.timetabler.Controls
{
    public class TeacherCheckItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        
        public Teacher Teacher { get; set; } = null!;
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class MultiSelectTeacherComboBox : UserControl
    {
        public static readonly DependencyProperty SelectedTeacherIdsProperty =
            DependencyProperty.Register(
                nameof(SelectedTeacherIds),
                typeof(List<Guid>),
                typeof(MultiSelectTeacherComboBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedTeacherIdsChanged));

        public static readonly RoutedEvent SelectionChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(SelectionChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(MultiSelectTeacherComboBox));

        public event RoutedEventHandler SelectionChanged
        {
            add => AddHandler(SelectionChangedEvent, value);
            remove => RemoveHandler(SelectionChangedEvent, value);
        }

        public List<Guid>? SelectedTeacherIds
        {
            get => (List<Guid>?)GetValue(SelectedTeacherIdsProperty);
            set => SetValue(SelectedTeacherIdsProperty, value);
        }

        public ObservableCollection<TeacherCheckItem> TeacherItems { get; } = new();

        private bool _isUpdating;

        public MultiSelectTeacherComboBox()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            TeacherMenu.Closed += TeacherMenu_Closed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshTeacherItems();
        }

        private static void OnSelectedTeacherIdsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MultiSelectTeacherComboBox control)
            {
                control.RefreshTeacherItems();
            }
        }

        private void RefreshTeacherItems()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            TeacherItems.Clear();
            foreach (var teacher in AppData.Current.Teachers)
            {
                var item = new TeacherCheckItem
                {
                    Teacher = teacher,
                    IsSelected = SelectedTeacherIds?.Contains(teacher.Id) ?? false
                };
                TeacherItems.Add(item);
            }

            TeacherMenu.ItemsSource = TeacherItems;
            UpdateDisplayText();
            _isUpdating = false;
        }

        private void DropDownButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshTeacherItems();
            TeacherMenu.PlacementTarget = DropDownButton;
            TeacherMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            TeacherMenu.IsOpen = true;
        }

        private void TeacherMenu_Closed(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;
            
            _isUpdating = true;
            
            if (SelectedTeacherIds != null)
            {
                SelectedTeacherIds.Clear();
                foreach (var item in TeacherItems.Where(i => i.IsSelected))
                {
                    SelectedTeacherIds.Add(item.Teacher.Id);
                }
            }

            UpdateDisplayText();
            _isUpdating = false;

            // Move focus away to trigger data refresh
            MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

            // Raise event to notify parent that selection changed
            RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
        }

        private void UpdateDisplayText()
        {
            var selectedCount = TeacherItems.Count(i => i.IsSelected);
            DisplayText.Text = selectedCount switch
            {
                0 => "Select teachers...",
                1 => TeacherItems.First(i => i.IsSelected).Teacher.Name,
                _ => $"{selectedCount} teachers selected"
            };
        }
    }
}

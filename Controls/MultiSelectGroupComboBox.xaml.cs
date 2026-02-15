using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using classique.timetabler.Data;
using classique.timetabler.Models;

namespace classique.timetabler.Controls
{
    public class GroupCheckItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        
        public Group Group { get; set; } = null!;
        
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

    public partial class MultiSelectGroupComboBox : UserControl
    {
        public static readonly DependencyProperty SelectedGroupIdsProperty =
            DependencyProperty.Register(
                nameof(SelectedGroupIds),
                typeof(List<Guid>),
                typeof(MultiSelectGroupComboBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedGroupIdsChanged));

        public static readonly RoutedEvent SelectionChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(SelectionChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(MultiSelectGroupComboBox));

        public event RoutedEventHandler SelectionChanged
        {
            add => AddHandler(SelectionChangedEvent, value);
            remove => RemoveHandler(SelectionChangedEvent, value);
        }

        public List<Guid>? SelectedGroupIds
        {
            get => (List<Guid>?)GetValue(SelectedGroupIdsProperty);
            set => SetValue(SelectedGroupIdsProperty, value);
        }

        public ObservableCollection<GroupCheckItem> GroupItems { get; } = new();

        private bool _isUpdating;

        public MultiSelectGroupComboBox()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            GroupMenu.Closed += GroupMenu_Closed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshGroupItems();
        }

        private static void OnSelectedGroupIdsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MultiSelectGroupComboBox control)
            {
                control.RefreshGroupItems();
            }
        }

        private void RefreshGroupItems()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            GroupItems.Clear();
            foreach (var group in AppData.Current.Groups)
            {
                var item = new GroupCheckItem
                {
                    Group = group,
                    IsSelected = SelectedGroupIds?.Contains(group.Id) ?? false
                };
                GroupItems.Add(item);
            }

            GroupMenu.ItemsSource = GroupItems;
            UpdateDisplayText();
            _isUpdating = false;
        }

        private void DropDownButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshGroupItems();
            GroupMenu.PlacementTarget = DropDownButton;
            GroupMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            GroupMenu.IsOpen = true;
        }

        private void GroupMenu_Closed(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;
            
            _isUpdating = true;
            
            if (SelectedGroupIds != null)
            {
                SelectedGroupIds.Clear();
                foreach (var item in GroupItems.Where(i => i.IsSelected))
                {
                    SelectedGroupIds.Add(item.Group.Id);
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
            var selectedCount = GroupItems.Count(i => i.IsSelected);
            DisplayText.Text = selectedCount switch
            {
                0 => "Select groups...",
                1 => GroupItems.First(i => i.IsSelected).Group.Name,
                _ => $"{selectedCount} groups selected"
            };
        }
    }
}

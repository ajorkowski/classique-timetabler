using System.Windows;
using System.Windows.Controls;

namespace classique.timetabler.Controls
{
    public partial class TimeOnlyPicker : UserControl
    {
        public static readonly DependencyProperty TimeProperty =
            DependencyProperty.Register(
                nameof(Time),
                typeof(TimeOnly),
                typeof(TimeOnlyPicker),
                new FrameworkPropertyMetadata(new TimeOnly(9, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimeChanged));

        public TimeOnly Time
        {
            get => (TimeOnly)GetValue(TimeProperty);
            set => SetValue(TimeProperty, value);
        }

        private bool _isUpdating;

        public TimeOnlyPicker()
        {
            InitializeComponent();

            // Populate hours (1-12)
            for (int i = 1; i <= 12; i++)
            {
                HourComboBox.Items.Add(i.ToString());
            }

            // Populate minutes (00, 15, 30, 45)
            MinuteComboBox.Items.Add("00");
            MinuteComboBox.Items.Add("15");
            MinuteComboBox.Items.Add("30");
            MinuteComboBox.Items.Add("45");

            Loaded += (s, e) => UpdateControlsFromTime();
        }

        private static void OnTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TimeOnlyPicker picker)
            {
                picker.UpdateControlsFromTime();
            }
        }

        private void UpdateControlsFromTime()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            var hour = Time.Hour;
            var minute = Time.Minute;
            var isPm = hour >= 12;

            // Convert to 12-hour format
            var hour12 = hour % 12;
            if (hour12 == 0) hour12 = 12;

            HourComboBox.SelectedItem = hour12.ToString();
            
            // Find closest minute option
            var minuteIndex = minute switch
            {
                < 8 => 0,   // 00
                < 23 => 1,  // 15
                < 38 => 2,  // 30
                < 53 => 3,  // 45
                _ => 0      // 00 (next hour, but we'll just show 00)
            };
            MinuteComboBox.SelectedIndex = minuteIndex;
            
            AmPmComboBox.SelectedIndex = isPm ? 1 : 0;

            _isUpdating = false;
        }

        private void Time_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (HourComboBox.SelectedItem == null || 
                MinuteComboBox.SelectedItem == null || 
                AmPmComboBox.SelectedIndex < 0) return;

            _isUpdating = true;

            var hour = int.Parse((string)HourComboBox.SelectedItem);
            var minute = int.Parse((string)MinuteComboBox.SelectedItem);
            var isPm = AmPmComboBox.SelectedIndex == 1;

            // Convert to 24-hour format
            if (isPm && hour != 12)
                hour += 12;
            else if (!isPm && hour == 12)
                hour = 0;

            Time = new TimeOnly(hour, minute);

            _isUpdating = false;
        }
    }
}

using SpaceBusinessPOS.Services;
using SpaceBusinessPOS.ViewModels;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SpaceBusinessPOS.Views
{
    public partial class LoginWindow : Window
    {
        private string _pin = "";

        public LoginWindow()
        {
            InitializeComponent();
            UpdateDots();
        }

        private void KeypadClick(object sender, RoutedEventArgs e)
        {
            if (_pin.Length >= 6) return;
            var btn = (System.Windows.Controls.Button)sender;
            _pin += btn.Tag?.ToString() ?? "";
            UpdateDots();
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private void BackspaceClick(object sender, RoutedEventArgs e)
        {
            if (_pin.Length > 0) _pin = _pin[..^1];
            UpdateDots();
        }

        private void ClearClick(object sender, RoutedEventArgs e)
        {
            _pin = "";
            UpdateDots();
        }

        private void LoginClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pin)) { ShowError("Enter your PIN"); return; }
            var user = UserService.Login(_pin);
            if (user == null)
            {
                ShowError("Invalid PIN. Try again.");
                _pin = "";
                UpdateDots();
                return;
            }
            AppState.CurrentUser = user;
            var main = new MainWindow();
            main.Show();
            Close();
        }

        private void ShowError(string msg)
        {
            ErrorText.Text = msg;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void UpdateDots()
        {
            PinDotsPanel.Children.Clear();
            for (int i = 0; i < 6; i++)
            {
                var dot = new Ellipse
                {
                    Width = 14,
                    Height = 14,
                    Margin = new Thickness(6, 0, 6, 0),
                    Fill = i < _pin.Length
                        ? new SolidColorBrush(Color.FromRgb(0xD9, 0x7B, 0x35))
                        : new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51)),
                    StrokeThickness = 1
                };
                PinDotsPanel.Children.Add(dot);
            }
        }
    }
}

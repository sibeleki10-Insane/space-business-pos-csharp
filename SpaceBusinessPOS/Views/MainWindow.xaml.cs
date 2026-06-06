using SpaceBusinessPOS.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SpaceBusinessPOS.Views
{
    public partial class MainWindow : Window
    {
        private string _activePage = "";

        public MainWindow()
        {
            InitializeComponent();
            UserNameText.Text = AppState.CurrentUser?.Name ?? "User";
            UserRoleText.Text = AppState.CurrentUser?.Role ?? "";
            Navigate("pos");
        }

        private void NavClick(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            Navigate(btn.Tag?.ToString() ?? "pos");
        }

        private void Navigate(string page)
        {
            _activePage = page;
            UpdateNavStyles();
            Page? view = page switch
            {
                "pos" => new POSPage(),
                "products" => new ProductsPage(),
                "categories" => new CategoriesPage(),
                "customers" => new CustomersPage(),
                "transactions" => new TransactionsPage(),
                "reports" => new ReportsPage(),
                "accounting" => new AccountingPage(),
                "users" => new UsersPage(),
                "receipt" => new ReceiptDesignerPage(),
                "settings" => new SettingsPage(),
                _ => new POSPage()
            };
            MainFrame.Navigate(view);
        }

        private void UpdateNavStyles()
        {
            var buttons = new[] { NavPOS, NavProducts, NavCategories, NavCustomers, NavTransactions, NavReports, NavAccounting, NavUsers, NavReceipt, NavSettings };
            foreach (var btn in buttons)
            {
                btn.Style = (string?)btn.Tag == _activePage
                    ? (Style)FindResource("NavButtonActive")
                    : (Style)FindResource("NavButton");
            }
        }

        private void LogoutClick(object sender, RoutedEventArgs e)
        {
            AppState.CurrentUser = null;
            var login = new LoginWindow();
            login.Show();
            Close();
        }
    }
}

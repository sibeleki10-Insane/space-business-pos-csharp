using SpaceBusinessPOS.Data;
using System.Windows;

namespace SpaceBusinessPOS
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Database.Initialize();
            var login = new Views.LoginWindow();
            login.Show();
        }
    }
}

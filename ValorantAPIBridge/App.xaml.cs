using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using ValorantAPIBridge.api;
using ValorantAPIBridge.whitelist;
using ValorantAPITest;

namespace ValorantAPIBridge
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public OriginWhitelist OriginWhitelist { set; get; } = new();
        public ObservableCollection<WhitelistItem> Whitelist { get; set; } = new();

        private static LockfileHandler _lockfileHandler;
        private static APIManager _apiManager;

        private void AppStartup(object sender, StartupEventArgs args)
        {
            OriginWhitelist.LoadFromFile(true);
            _lockfileHandler = new LockfileHandler();
            
            _apiManager = new APIManager(_lockfileHandler, OriginWhitelist);
            Task.Factory.StartNew(() =>
            {
                OriginWhitelist.LoadFromFile(true);

                _apiManager.startListening();
            });
        }
    }
}
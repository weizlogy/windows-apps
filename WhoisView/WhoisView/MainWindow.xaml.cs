using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WhoisView
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window {
        /// <summary>
        /// constructor.
        /// </summary>
        public MainWindow() {
            // auto generate.
            InitializeComponent();
            this.Top = 0;
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width;
            // add events.
            this.MouseLeftButtonDown += (s, e) => { this.DragMove(); };
            this.KeyUp += (s, e) => {
                switch (e.Key) {
                    case Key.Escape:
                        Application.Current.Shutdown();
                        break;
                }
            };
            this.Loaded += (s, e) => { this.CheckWhois(); };
            NetworkChange.NetworkAddressChanged += (s, e) => { this.CheckWhois(); };
        }

        protected async void CheckWhois() {
            var ip = "";
            var country = "";
            try {
                var myip = await this.GetMyIpAddress();
                ip = myip.IP;
                country = myip.CountryName;
            } catch (Exception ex) {
                ip = ex.GetType().Name;
                country = ex.Message;
            }
            this.TxtMyIp.Dispatcher.Invoke(() => {
                this.TxtMyIp.Text = ip;
                this.TxtMyIpCountry.Text = country;
            });
        }

        protected async Task<MyIp> GetMyIpAddress() {
            using (var client = new HttpClient()) {
                var message = await client.GetAsync("http://api.ipaddress.com/iptocountry?format=json");
                var stream = await message.Content.ReadAsStreamAsync();
                var myip = (MyIp)new DataContractJsonSerializer(typeof(MyIp)).ReadObject(stream);
                // Console.WriteLine("{0}@{1}", myip.IP, myip.CountryName);
                return myip;
            }
        }
    }
}

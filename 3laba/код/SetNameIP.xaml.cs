using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace _3laba_P2P
{
    /// <summary>
    /// Логика взаимодействия для SetNameIP.xaml
    /// </summary>
    public partial class SetNameIP : Window
    {
        public string SelectedIP { get; private set; }
        public string UserName { get; private set; }

        public string BroadcastIP { get; private set; }

        public static MainWindow MW { get; private set; }
        List<IPAddress> ips = new List<IPAddress>();
        List<IPAddress> broadcasts = new List<IPAddress>();

        public SetNameIP()
        {
            InitializeComponent();
            MW = new MainWindow(this);
            LoadLocalIPs();
        }

        private void LoadLocalIPs()
        {
            ips = new List<IPAddress>();
            broadcasts = new List<IPAddress>();

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;

                foreach (UnicastIPAddressInformation ipInfo in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ipInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ips.Add(ipInfo.Address);
                        broadcasts.Add(GetBroadcastAddress(ipInfo.Address, ipInfo.IPv4Mask));
                    }
                }
            }

            // Например, ComboBox для IP, TextBlock для отображения соответствующего broadcast
            IpComboBox.ItemsSource = ips;
            if (ips.Count > 0)
            {
                IpComboBox.SelectedIndex = 0;
                BroadcastLabel.Content = broadcasts[0].ToString();
            }
        }

        private void InputChanged(object sender, EventArgs e)
        {
            // Кнопка активна только если имя не пустое и IP выбран
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(NameTextBox.Text) && IpComboBox.SelectedItem != null;
            if (IpComboBox.SelectedItem != null) BroadcastLabel.Content = broadcasts[IpComboBox.SelectedIndex].ToString();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            foreach(Node node in NetworkOperations.NodesUI)
            {
                node.OnClose();
            }
            SelectedIP = IpComboBox.SelectedItem.ToString();
            UserName = NameTextBox.Text.Trim();
            UserName.Replace("|", "/");
            BroadcastIP = broadcasts[IpComboBox.SelectedIndex].ToString();

            //MessageBox.Show($"Выбрано имя: {UserName}\nIP: {SelectedIP}", "Выбор подтверждён");
            NetworkOperations.Initial(SelectedIP, UserName, BroadcastIP);
            MW.Show();
            MW.NodesLabel.Content = "Узлы (ВЫ: " + SelectedIP + " | " + UserName + "\nШ-ние: " + BroadcastIP + " )";
            NetworkOperations.Messages.Add(new Message($"{NetworkOperations.MyIP}, {NetworkOperations.MyName}", "--ВЫ (ПЕРЕ)ПОДКЛЮЧИЛИСЬ--", 3));
            MW.UpdateChat();


            this.Hide();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Environment.Exit(0);

        }


        public static IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
        {
            // Проверка: если loopback, возвращаем 127.0.0.1
            byte firstOctet = address.GetAddressBytes()[0];
            if (firstOctet == 127)
            {
                //return IPAddress.Parse("255.255.255.255");
                return IPAddress.Parse("239.0.0.1");
            }
                

            byte[] ipBytes = address.GetAddressBytes();
            byte[] maskBytes = subnetMask.GetAddressBytes();

            if (ipBytes.Length != maskBytes.Length)
                throw new ArgumentException("IP и маска должны быть одного размера");

            byte[] broadcastBytes = new byte[ipBytes.Length];

            for (int i = 0; i < ipBytes.Length; i++)
            {
                broadcastBytes[i] = (byte)(ipBytes[i] | (~maskBytes[i]));
            }

            return new IPAddress(broadcastBytes);
        }

        private void Window_Closing_1(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (MW == null) return;
            MW.ChB.IsChecked = true;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (MW == null) return;
            MW.ChB.IsChecked = false;
        }
    }
}

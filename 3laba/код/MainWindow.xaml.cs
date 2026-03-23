using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace _3laba_P2P
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SetNameIP SNI;
        public ObservableCollection<Node> Nodes = NetworkOperations.NodesUI;
        private Node _selectedNode;
        public MainWindow(SetNameIP SNI)
        {
            NetworkOperations.MW = this;
            InitializeComponent();
            DataContext = this;

            this.SNI = SNI;

            Task.Run(async () =>
            {
                await Task.Delay(30000); // ждём 30 секунд
                await UDPcontroll.SendBroadcast();
            });

        }

        private void RefrashNodesButton_Click(object sender, RoutedEventArgs e)
        {
            UDPcontroll.SendBroadcast();
        }

        private void ChangeNameIP_Click(object sender, RoutedEventArgs e)
        {
            SNI.Show();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            
            Environment.Exit(0);
            
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UDPcontroll.dbg = true;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
             UDPcontroll.dbg = false;
        }
        public void WriteEvent(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (message != null)
                    StoryCurrent.Text += "\n\n" + DateTime.Now + "\t" + message;
                else
                    MessageBox.Show("NULL MESSAGE");
            });
        }

        private void NodesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedNode = NodesList.SelectedItem as Node;
            //UpdateChat();
        }

        public void UpdateChat()
        {
            //if (_selectedNode == null)
            //{
            //    ChatField.Text = "";
            //    return;
            //}

            StringBuilder sb = new StringBuilder();

            //foreach (var msg in _selectedNode.messages)
            //{
            //    sb.AppendLine($"[{msg.time:HH:mm:ss}] {msg.Name}: {msg.text}");
            //}

            foreach (var msg in NetworkOperations.Messages)
            {
                sb.AppendLine($"[{msg.time:HH:mm:ss}] [{msg.Name}]: {msg.text}");
            }

            ChatField.Text = sb.ToString();
        }

        private void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            //if (_selectedNode == null) return;

            string text = ChatInput.Text.Trim();
            text.Replace("|", "/");
            if (string.IsNullOrEmpty(text)) return;

            //_selectedNode.SendMessage(text, 3);
            foreach (Node node in NetworkOperations.NodesUI) {
                node.SendMessage(text, 3);
                node.messages.Add(new Message("Me", text));
            }
            WriteEvent("[" + NetworkOperations.MyName + "]: " + text);
            NetworkOperations.Messages.Add(new Message($"{NetworkOperations.MyIP}, {NetworkOperations.MyName}", $"{text}"));
            //_selectedNode.messages.Add(new Message("Me", text));
            ChatInput.Clear();
            UpdateChat();
        }

        public void SubscribeNode(Node node)
        {
            node.OnMessageReceived2 += (msg) =>
            {
                Dispatcher.Invoke(() =>
                {
                    //if (node == _selectedNode)
                        UpdateChat();
                });
            };
        }
        public void WritePrevStory(string text1, string log)
        {
            //Dispatcher.Invoke(() =>
            //{
            //    StoryPrevLabel.Content = text1;
            //    StoryPrev.Text = log;
            //});
        }

        private void GotNodeHystory_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode == null) return;
            NetworkOperations.HavePrevHystory = false;
            _selectedNode.SendMessage("Recall for log", 4);
        }

        private void ResetChat_Click(object sender, RoutedEventArgs e)
        {
            NetworkOperations.Messages.Clear();
            UpdateChat();
        }
    }
}
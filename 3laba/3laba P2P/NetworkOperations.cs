using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Packaging;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;


//1 - alive, 2 - disconnect (явно указать вызвать OnClose), 3 - message, 4 - запрос на передачу лога (будет копироавть StoryCurrent из MW), 5 - ответ с логом (будет записываться в StoryPrev в MW)
namespace _3laba_P2P
{

    public class Message
    {
        public int type = 0; //текстовое, 1--отладочное
        public DateTime time;
        public string Name;
        public string text;
        public Message(string Name, string text, int type = 0)
        {
            this.Name = Name;
            this.text = text;
            time = DateTime.Now;
            this.type = type;
        }
    }
    public class Node
    {
        public MainWindow MW = NetworkOperations.MW;
        public string Name { get; set; }
        public string IP { get; set; }
        public int Port;
        public Guid Id;
        public int MyPort;

        public Socket MyClient;
        public Socket Client;

        private bool _closed = false;

        public ObservableCollection<Message> messages = new ObservableCollection<Message>();

        public DateTime LastContact { get; private set; }

        public event Action<byte, string> OnMessageReceived; // тип, текст

        public Node(string name, string ip, int port, Guid id)
        {
            Name = name;
            IP = ip;
            Port = port;
            Id = id;
            LastContact = DateTime.Now;

            ConnectionAlive();
            OnMessageReceived += HandlePacket;
        }
        public event Action<Message> OnMessageReceived2;
        

        public int StartServer()
        {
            try
            {
                IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(NetworkOperations.MyIP), 0);
                MyClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                MyClient.Bind(ipPoint);
                MyPort = ((IPEndPoint)MyClient.LocalEndPoint).Port;

                MyClient.Listen(10);
                Task.Run(() => {
                    try
                    {
                        Client = MyClient.Accept();
                        var remoteEP = (IPEndPoint)Client.RemoteEndPoint;
                        string ip = remoteEP.Address.ToString();
                        int port = remoteEP.Port;

                        if (MW != null) MW.WriteEvent($"TCP Соединение с {ip}:{port}| {Name} установлено");
                        NetworkOperations.Messages.Add(new Message($"{NetworkOperations.MyIP}, {NetworkOperations.MyName}", $"Узел {IP} имя {Name} подключён", 1));
                        if (!NetworkOperations.HavePrevHystory) SendMessage("", 4);
                        OnMessageReceived2?.Invoke(null);
                        StartReading(Client);
                    }
                    catch {
                        if(MyPort == -1) OnClose();
                    }
                    
                });

                if (UDPcontroll.dbg && MW != null) MW.WriteEvent("Севрер запущен, ожидаю клиента");
                return MyPort;
            }
            catch (Exception ex)
            {
                if (UDPcontroll.dbg && MW != null) MW.WriteEvent($"Ошибка Запуска TCPсервера для {IP}|{Name}: " + ex.Message);
                OnClose();
                return -1;
            }

        }
        public async void StartClient(int Port)
        {
            try
            {
                if (Client != null)
                {
                    if(Client.Connected) Client.Shutdown(SocketShutdown.Both);
                    Client.Close();
                    
                }
                

                IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(IP), Port);
         
                Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Client.Bind(new IPEndPoint(IPAddress.Parse(NetworkOperations.MyIP), 0));
                Client.Connect(ipPoint);


                if(MW != null) MW.WriteEvent($"TCP Подключение к {IP}:{Port}|{Name} установлено");
                NetworkOperations.Messages.Add(new Message($"{NetworkOperations.MyIP}, {NetworkOperations.MyName}", $"Узел {IP} имя {Name} подключён", 1));
                if (!NetworkOperations.HavePrevHystory) SendMessage("", 4);
                OnMessageReceived2?.Invoke(null);
                StartReading(Client);

                if (MyClient != null)
                {
                    Thread.Sleep(1000);
                    if(MyClient.Connected) MyClient.Shutdown(SocketShutdown.Both);
                    MyClient.Close();
                    if (UDPcontroll.dbg && MW != null) MW.WriteEvent($"Сервер к {IP}|{Name} закрыт");
                }

            }
            catch (Exception ex) 
            {
                if (UDPcontroll.dbg && MW != null) MW.WriteEvent($"Ошибка TCPподключения к {IP}|{Name}" + ex.Message);
            }
        }





        public void SendMessage(string message, byte type)
        {
            if (Client == null || !Client.Connected) return;

            try
            {

                byte[] mesage_b = Encoding.UTF8.GetBytes(message);
                int length = (int)mesage_b.Length;

                byte[] packet = new byte[1 + 2 + mesage_b.Length];
                packet[0] = type;
                packet[1] = (byte)((length >> 8) & 0xFF); // старший байт длины
                packet[2] = (byte)(length & 0xFF);        // младший байт длины
                Array.Copy(mesage_b, 0, packet, 3, mesage_b.Length);

                Client.Send(packet);
            }
            catch (Exception ex)
            {
                if (UDPcontroll.dbg && MW != null) MW.WriteEvent($"Ошибка SendMessage: {ex.Message}");
            }
        }

        public void StartReading(Socket socket)
        {
            Task.Run(() => reading_loop(socket));
        }

        private async void reading_loop(Socket socket)
        {
            byte[] MyHeader = new byte[3];
            while (socket.Connected)
            {
                try
                {
                    int read = 0;
                    while (read < 3)
                    {
                        int r = await socket.ReceiveAsync(MyHeader.AsMemory(read, 3 - read), SocketFlags.None);
                        if (r == 0) throw new Exception("Соединение закрыто");
                        read += r;
                    }
                    byte type = MyHeader[0];
                    ushort length = (ushort)((MyHeader[1] << 8) | MyHeader[2]);

                    byte[] buffer = new byte[length];
                    int received = 0;
                    while (received < length)
                    {
                        int r = await socket.ReceiveAsync(buffer.AsMemory(received, length - received), SocketFlags.None);
                        if (r == 0) throw new Exception("Соединение закрыто");
                        received += r;
                    }

                    string text = Encoding.UTF8.GetString(buffer);
                    OnMessageReceived?.Invoke(type, text);
                }
                catch (Exception ex)
                {
                    if (MW != null) MW.WriteEvent("Ошибка чтения: " + ex.Message);
                    OnClose();
                    break;
                }
            }
        }

        public void HandlePacket(byte type, string text)
        {
            // Обновляем время последнего контакта
            LastContact = DateTime.Now;

            switch (type)
            {
                case 1: // Alive
                    break;

                case 2: // Disconnect
                    OnClose();
                    break;

                case 3: // Обычное
                    Message msg = new Message(Name, text);
                    messages.Add(msg);
                    if (MW != null) MW.WriteEvent($"[{Name}] {text}");
                    NetworkOperations.Messages.Add(new Message($"{IP}, {Name}", $"{text}"));
                    OnMessageReceived2?.Invoke(msg);

                    break;

                case 4: // Запрос на лог
                    if (MW != null)
                    {
                        //string log = "\n" + MW.StoryCurrent;
                        string log = NetworkOperations.MessageStoryToText();
                        SendMessage(log, 5);
                    }
                    break;

                case 5: // Ответ с логом
                    if (MW != null)
                    {
                        if (!NetworkOperations.HavePrevHystory)
                        {
                            NetworkOperations.TextStoryToMessages(text);
                            //MW.WritePrevStory("История с узла:" + Name, text);
                            NetworkOperations.HavePrevHystory = true;
                            OnMessageReceived2?.Invoke(null);
                        }
                        
                    }
                    break;

                default:
                    if (MW != null) MW.WriteEvent($"Неизвестный тип пакета {type} от {Name}");
                    break;
            }
        }

        public void OnClose()
        {
            if (_closed) return;
            _closed = true;
            try
            {
                NetworkOperations.Nodes.TryRemove(Id.ToString(), out Node emptyNode);
                NetworkOperations.UpdUI();
                if (Client != null && Client.Connected) Client.Shutdown(SocketShutdown.Both);
                if (Client != null) Client.Close();
                if (MyClient != null && MyClient.Connected) MyClient.Shutdown(SocketShutdown.Both);
                if (MyClient != null) MyClient.Close();
            }
            finally
            {
                if (MW != null) MW.WriteEvent($"Отключён узел {IP} имя {Name}");
                NetworkOperations.Messages.Add(new Message($"{NetworkOperations.MyIP}, {NetworkOperations.MyName}", $"Узел {IP} имя {Name} отключён", 1));
                OnMessageReceived2?.Invoke(null);
            }
            
        }
        ~Node()
        {
            OnClose();
        }


        public void ConnectionAlive(int intervalMs = 5000, int timeoutMs = 20000)
        {
            Task.Run(async () =>
            {
                
                while (true)
                {
                    SendMessage("Check for alive", 1);
                    await Task.Delay(intervalMs);
                    try
                    {
                        if (Client == null || !Client.Connected)
                        {
                            if (MW != null) MW.WriteEvent($"ConnectionAlive: узел {IP}|{Name} недоступен");
                            OnClose();
                            break;
                        }

                        if ((DateTime.Now - LastContact).TotalMilliseconds > timeoutMs)
                        {
                            if (MW != null) MW.WriteEvent($"ConnectionAlive: узел {IP}|{Name} не отвечает");
                            OnClose();
                            break;
                        }
                    }
                    catch
                    {
                        OnClose();
                        break;
                    }
                }
            });
        }

    }

    public static class NetworkOperations
    {
        public static MainWindow MW = null;
        public static string MyIP;
        public static string MyName;
        

        public static Guid MyId = Guid.NewGuid();

        public static int BroadcastPort = 8888;
        public static string BroadcastIP = "255.255.255.255";

        public static ConcurrentDictionary<string, Node> Nodes = new();
        public static ObservableCollection<Node> NodesUI = new();

        public static bool HavePrevHystory = false;

        public static List<Message> Messages = new();

        public static void UpdUI()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                NodesUI.Clear();

                foreach (var node in Nodes.Values)
                {
                    NodesUI.Add(node);
                }
                foreach (var node in NodesUI)
                {
                    MW.SubscribeNode(node);
                }

            }));
        }

        public static string MessageStoryToText()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("|");
            foreach(Message mes in Messages)
            {
                if (mes.type == 3) continue;
                sb.Append($"{mes.type}|{mes.time.ToString()}|{mes.Name}|{mes.text}|");
            }
            return sb.ToString();
        }
        public static void TextStoryToMessages(string text)
        {

            string[] parts = text.Split('|');

            for (int i = 0; i < parts.Length; i++)
            {
                if (i >= parts.Length) break;
                if (string.IsNullOrEmpty(parts[i])) continue;

                int type = Convert.ToInt32(parts[i]);

                i++;
                if (i >= parts.Length) break;
                DateTime dt = DateTime.ParseExact(parts[i], "dd.MM.yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

                i++;
                if (i >= parts.Length) break;
                string name = parts[i];

                i++;
                if (i >= parts.Length) break;
                string text1 = parts[i];

                Message me = new(name, text1, type);
                me.time = dt;

                bool have_same = false;

                foreach (var existing in Messages)
                {
                    if (existing.Name == me.Name &&
                        existing.text == me.text &&
                        Math.Abs((existing.time - me.time).TotalSeconds) <= 2)
                    {
                        have_same = true;
                        break;
                    }
                }

                if (have_same)
                    continue;

                Messages.Add(me);
            }

            Messages.Sort((a, b) => a.time.CompareTo(b.time));
        }

        public static void Initial(string ip, string name, string broadcastIP)
        {
            Nodes = new();
            UpdUI();
            if(UDPcontroll.listener != null) UDPcontroll.listener.Close();
            MyIP = ip;
            MyName = name;
            BroadcastIP = broadcastIP;
            
            if(UDPcontroll.listener != null)
            {
                UDPcontroll.StartListening();
            }
            else
            {
                UDPcontroll.StartListening();
                UDPcontroll.SendBroadcast();
            }
            Thread.Sleep(100);

        }

    }
}

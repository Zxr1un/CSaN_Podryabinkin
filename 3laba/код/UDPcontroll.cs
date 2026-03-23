using _3laba_P2P;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;


//Структура пакетов UDP
//BС|MyID|MyIP|MyName Широковещательный
//ANSW|MyID|MyIP|MyName|MyPort|DestID|DestIP|DestName Ответ на широковещательный
//ANSW2|MyID|MyIP|MyName|MyPort|DestID|DestIP|DestName Приказ на подключение к себе


public static class UDPcontroll
{
    public static MainWindow MW = NetworkOperations.MW;
    public static bool dbg = true;
    public static UdpClient listener = null;
    //public static Socket listener = null;

    public static void StartListening()
    {

        listener = new UdpClient();

        listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Client.Bind(new IPEndPoint(IPAddress.Any, NetworkOperations.BroadcastPort));

        if (NetworkOperations.BroadcastIP == "239.0.0.1")
        {
            listener.JoinMulticastGroup(IPAddress.Parse("239.0.0.1"));
        }
        Task.Run(async () =>
        {
            while (true)
            {
                var result = await listener.ReceiveAsync();
                string message = Encoding.UTF8.GetString(result.Buffer);

                IPEndPoint target = result.RemoteEndPoint;
                string[]? parts = message.Split('|');
                target.Address = IPAddress.Parse(parts[2]);
                target.Port = NetworkOperations.BroadcastPort;
                //if (dbg && MW != null) MW.WriteEvent("Получен пакет");
                HandlePacket(message, target);
            }
        });

        //listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        //listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        //listener.Bind(new IPEndPoint(IPAddress.Any, NetworkOperations.BroadcastPort));
        //Task.Run(async () =>
        //{
        //    byte[] buffer = new byte[65535]; // максимальный размер UDP пакета
        //    EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        //    while (true)
        //    {
        //        try
        //        {
        //            SocketReceiveFromResult result = await listener.ReceiveFromAsync(buffer, SocketFlags.None, remoteEP);
        //            int received = result.ReceivedBytes;
        //            EndPoint remoteEndPoint = result.RemoteEndPoint;
        //            string message = Encoding.UTF8.GetString(buffer, 0, received);

        //            // Разбираем пакет
        //            string[]? parts = message.Split('|');

        //            // Создаём target с IP и портом
        //            IPEndPoint target = new IPEndPoint(IPAddress.Parse(parts[2]), NetworkOperations.BroadcastPort);

        //            HandlePacket(message, target);
        //        }
        //        catch (Exception ex)
        //        {
        //            if (UDPcontroll.dbg && NetworkOperations.MW != null) MW.WriteEvent($"Ошибка приёма UDP: {ex.Message}");
        //        }
        //    }
        //});
    }

    // Broadcast
    public static async Task SendBroadcast()
    {
        using var client = new UdpClient();

        client.EnableBroadcast = true;

        string msg = $"BC|{NetworkOperations.MyId}|{NetworkOperations.MyIP}|{NetworkOperations.MyName}";
        byte[] data = Encoding.UTF8.GetBytes(msg);
        await client.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Parse(NetworkOperations.BroadcastIP), NetworkOperations.BroadcastPort));
        if (dbg && MW != null) MW.WriteEvent("Отправлен BC пакет");
        Thread.Sleep(200);
        //using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        //{
        //    //socket.Bind(new IPEndPoint(IPAddress.Parse(NetworkOperations.MyIP), 0));
        //    if (NetworkOperations.BroadcastIP == "239.0.0.1")
        //    {
        //        //socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
        //        NetworkOperations.BroadcastIP = "255.255.255.255";
        //    }
        //    //else socket.EnableBroadcast = true;
        //    socket.EnableBroadcast = true;
        //    IPEndPoint RemoteIP = new(IPAddress.Parse(NetworkOperations.BroadcastIP), NetworkOperations.BroadcastPort);
        //    socket.SendTo(data, RemoteIP);
        //    if (dbg && MW != null) MW.WriteEvent("Отправлен BC пакет");
        //    //Thread.Sleep(200);

        //}
    }

    //  Ответ
    private static async Task SendAnswer(string DestID, string DestIp,string DestName, int MyPort)
    {
        using var client = new UdpClient();
        string msg = $"ANSW|{NetworkOperations.MyId}|{NetworkOperations.MyIP}|{NetworkOperations.MyName}|{MyPort}|{DestID}|{DestIp}|{DestName}";
        byte[] data = Encoding.UTF8.GetBytes(msg);
        await client.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Parse(NetworkOperations.BroadcastIP), NetworkOperations.BroadcastPort));
        if (dbg && MW != null) MW.WriteEvent("Отправлен ANSW пакет");
        Thread.Sleep(200);
        //using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        //{
        //    //socket.Bind(new IPEndPoint(IPAddress.Parse(NetworkOperations.MyIP), 0));
        //    IPEndPoint RemoteIP = new(IPAddress.Parse(DestIp), NetworkOperations.BroadcastPort);
        //    socket.SendTo(data, RemoteIP);
        //    if (dbg && MW != null) MW.WriteEvent("Отправлен ANSW пакет");
        //    //Thread.Sleep(200);

        //}
    }

    private static async Task SendAnswer2(string DestID, string DestIp, string DestName, int MyPort)
    {
        using var client = new UdpClient();
        string msg = $"ANSW2|{NetworkOperations.MyId}|{NetworkOperations.MyIP}|{NetworkOperations.MyName}|{MyPort}|{DestID}|{DestIp}|{DestName}";
        byte[] data = Encoding.UTF8.GetBytes(msg);
        await client.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Parse(NetworkOperations.BroadcastIP), NetworkOperations.BroadcastPort));
        if (dbg && MW != null) MW.WriteEvent("Отправлен ANSW2 пакет");
        Thread.Sleep(200);
        //using(Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        //{
        //    IPEndPoint RemoteIP = new(IPAddress.Parse(DestIp), NetworkOperations.BroadcastPort);
        //    socket.SendTo(data, RemoteIP);
        //    if (dbg && MW != null) MW.WriteEvent("Отправлен ANSW2 пакет");
        //    Thread.Sleep(200);

        //}

    }


    private static async void HandlePacket(string message, IPEndPoint remoteEP)
    {
        var parts = message.Split('|');

        if (parts.Length < 4) return;

        string type = parts[0];
        Guid remoteId = Guid.Parse(parts[1]);
        string remoteIP = parts[2];
        string remoteName = parts[3];

        if (remoteId == NetworkOperations.MyId)
        {
            if(dbg && MW != null) MW.WriteEvent($"Словил свой же пакет: {message}");
            return;
        }

        string key = remoteId.ToString();

        if (type == "BC")
        {
            if (dbg && MW != null) MW.WriteEvent($"Поймал {remoteIP} имя {remoteName}");
            if (NetworkOperations.Nodes.ContainsKey(remoteId.ToString())) return; //Либо уже зарегистрирован, либо ждём подтверждения от него
            if (MW != null) MW.WriteEvent($"Обнаружен {remoteIP} имя: {remoteName}");
            Node n_node = new Node(remoteName, remoteIP, -1, remoteId);
            NetworkOperations.Nodes[remoteId.ToString()] = n_node;
            NetworkOperations.UpdUI();

            int MyPort = n_node.StartServer();
            n_node.MyPort = MyPort;
            Thread.Sleep(200);
            await SendAnswer(remoteId.ToString(), remoteIP, remoteName, MyPort);
        }
        else if (type == "ANSW")
        {
            if (parts.Length < 5) return;
            if (String.Compare(parts[5], NetworkOperations.MyId.ToString()) != 0) return;

            if (dbg && MW != null) MW.WriteEvent(message);
            Thread.Sleep(200);

            int remotePort = int.Parse(parts[4]);

            if(remotePort == -1) //Если у того сервер не запустился
            {
                NetworkOperations.Nodes.TryRemove(key, out Node empt_node);
                NetworkOperations.UpdUI();
                return;
            }

            if (NetworkOperations.Nodes.ContainsKey(key))
            {
                if (NetworkOperations.Nodes[key].Port != -1)
                {
                    if (dbg && MW != null) MW.WriteEvent("Такой узел уже есть");
                    return;
                }
            }

            Node node;
            if (!NetworkOperations.Nodes.ContainsKey(key))
            {
                node = new Node(remoteName, remoteIP, remotePort, remoteId);
                NetworkOperations.Nodes[key] = node;
                NetworkOperations.UpdUI();
            }
            else node = NetworkOperations.Nodes[key];

            if (NetworkOperations.MyId.CompareTo(remoteId) > 0)
            {
                int MyPort = node.StartServer();
                await SendAnswer2(remoteId.ToString(), remoteIP, remoteName, MyPort);
            }
            else
            {
                node.StartClient(remotePort);
            }
            // иначе ждём входящее подключение
        }
        else if (type == "ANSW2")
        {
            if (parts.Length < 5) return;
            if (String.Compare(parts[5], NetworkOperations.MyId.ToString()) != 0) return;
            if (dbg && MW != null) MW.WriteEvent(message);
            Thread.Sleep(200);
            if (!NetworkOperations.Nodes.ContainsKey(key)) return;
            Node node = NetworkOperations.Nodes[key];
            int remotePort = int.Parse(parts[4]);
            if(remotePort == -1)
            {
                NetworkOperations.Nodes.TryRemove(key, out Node empt_node);
                NetworkOperations.UpdUI();
                return;
            }
            node.StartClient(remotePort);
            
        }
    }
}
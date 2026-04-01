using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;

namespace ProxyLaba
{
    public class MyProxy
    {
        public string IP ="127.0.0.2";
        public int port = 8888;

        public Socket socket_browser;

        public MyProxy(string  ip, int port)
        {
            IP = ip;
            this.port = port;
        }


        public async Task StartAsync()
        {
            socket_browser = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket_browser.Bind(new IPEndPoint(IPAddress.Parse(IP), port));
            socket_browser.Listen(10);

            Console.WriteLine($"Proxy started on {IP}:{port}");

            while (true)
            {
                Socket client = await socket_browser.AcceptAsync();
                
                Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(Socket client)
        {
            try
            {
                byte[] buffer = new byte[8192];
                StringBuilder requestBuilder = new StringBuilder();

                while (client != null && client.Connected)
                {
                    int bytesRead = await client.ReceiveAsync(buffer);
                    if (bytesRead <= 0) return;

                    string chunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    requestBuilder.Append(chunk);

                    // Проверяем, пришел ли полный HTTP-заголовок
                    if (requestBuilder.ToString().Contains("\r\n\r\n"))
                    {
                        string request = requestBuilder.ToString();
                        requestBuilder.Clear(); // готовим для следующего запроса
                        await HTTP_Handle(client, request);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Client error: " + ex.Message);
            }
        }

        public async Task HTTP_Handle(Socket client, string http)
        {
            // 1 Парсим стартовую строку
            string[] lines = http.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0) return;
            string[] startLine = lines[0].Split(' ');
            if (startLine.Length < 3) return;
            string method = startLine[0];
            string fullUrl = startLine[1];
            string version = startLine[2];

            // 2 Парсим заголовки
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int headerEndIndex = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    headerEndIndex = i;
                    break;
                }

                int idx = lines[i].IndexOf(':');
                if (idx > 0)
                {
                    string key = lines[i].Substring(0, idx).Trim();
                    string value = lines[i].Substring(idx + 1).Trim();
                    headers[key] = value;
                }
            }

            string host = null;
            if (headers.ContainsKey("Host"))
            {
                host = headers["Host"];
            }
            if (host == null)
            {
                await client.SendAsync(Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n"), SocketFlags.None);
                return;
            }

            string targetHost = host;
            int targetPort = 80;
            if (host.Contains(":"))
            {
                string[] parts = host.Split(':');
                targetHost = parts[0];
                int.TryParse(parts[1], out targetPort);
            }

            try
            {
                string address_for_blackList = "/";
                try
                {
                    address_for_blackList = new Uri(fullUrl).Host;
                    if (checkBlackList(address_for_blackList))
                    {

                        Console.WriteLine("BalckList capture: " + targetHost);

                        string message = "BalckList";
                        string response = $"HTTP/1.1 403 Forbidden\r\nContent-Type: text/plain\r\nContent-Length: {Encoding.UTF8.GetByteCount(message)}\r\n" +
                                          $"\r\n{message}";
                        await client.SendAsync(Encoding.ASCII.GetBytes(response), SocketFlags.None);
                        Task.Delay(200);
                        client.Shutdown(SocketShutdown.Both);
                        client.Close();
                        await Task.Delay(200);
                        return;
                    }
                }
                catch(Exception ex) {
                    Console.WriteLine("BlackList Error: " + ex.Message);
                }
                

                using (Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {

                    var addresses = await Dns.GetHostAddressesAsync(targetHost);

                    var ipv4Addresses = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToArray();
                    if (ipv4Addresses.Length == 0)
                    {
                        Console.WriteLine("DNS не смог получить IPv4: " + targetHost);
                        await client.SendAsync(
                            Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n"),
                            SocketFlags.None
                        );
                        return;
                    }

                    // Берём первый IPv4
                    var targetIP = ipv4Addresses[0];

                    //if (addresses.Length == 0)
                    //{
                    //    Console.WriteLine("Cannot resolve host: " + targetHost);
                    //    await client.SendAsync(Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n"), SocketFlags.None);
                    //    return;
                    //}

                    IPEndPoint remIP = new IPEndPoint(targetIP, targetPort);
                    await server.ConnectAsync(remIP);

                    string path = "/";
                    
                    try
                    {
                        path = new Uri(fullUrl).PathAndQuery; //ghjdthrf yf gjkysq genm
                        
                    }
                    catch
                    {
                    }
                    string newRequest = $"{method} {path} {version}\r\n";
                    

                    //остальные заголовки
                    for (int i = 1; i < headerEndIndex; i++)
                    {
                        newRequest += lines[i] + "\r\n";
                    }
                    newRequest += "\r\n"; // конец заголовков
                   
                    byte[] requestBytes = Encoding.ASCII.GetBytes(newRequest);
                    await server.SendAsync(requestBytes, SocketFlags.None);

                    // Потоковое пересылание ответа клиенту
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    bool firstChunk = true;
                    string statusLine = "";
                    while ((bytesRead = await server.ReceiveAsync(buffer, SocketFlags.None)) > 0)
                    {
                        string debug_buf = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        await client.SendAsync(new ArraySegment<byte>(buffer, 0, bytesRead), SocketFlags.None);
                        //сам лог
                        if (firstChunk)
                        {
                            string responseText = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                            string[] responseLines = responseText.Split(new[] { "\r\n" }, StringSplitOptions.None);
                            if (responseLines.Length > 0)
                            {
                                statusLine = responseLines[0];
                                Logger.AddMessage($"{fullUrl} -- {statusLine}");
                            }
                            firstChunk = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Proxy error: " + ex.Message);
                if (client.Connected)
                {
                    await client.SendAsync(
                        Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n"),
                        SocketFlags.None
                        
                    );
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                    await Task.Delay(200);
                }
            }
        }


        public bool checkBlackList(string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return false;
            string domainToCheck = NormAddress(search);
            try
            {
                string[] lines = File.ReadAllLines("blacklist.txt");
                foreach (var line in lines)
                {
                    string blackDomain = line.Trim().ToLower();
                    if (string.IsNullOrEmpty(blackDomain)) continue;
                    if (domainToCheck.EndsWith(blackDomain)) return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Blacklist check error: " + ex.Message);
            }

            return false;
        }
        private string NormAddress(string host)
        {
            host = host.ToLower().Trim();
            if (host.StartsWith("http://")) host = host.Substring(7);
            else if (host.StartsWith("https://")) host = host.Substring(8);
            if (host.StartsWith("www.")) host = host.Substring(4);
            return host;
        }

    }

    public static class Logger
    {
        private static string logFilePath;
        public static void Init(string path)
        {
            logFilePath = path;
            try
            {
                using (var fs = new FileStream(logFilePath, FileMode.Create, FileAccess.Write))
                using (var writer = new StreamWriter(fs))
                {
                    writer.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} --- Logger initialized ---");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Logger Init error: " + ex.Message);
            }
        }
        public static void AddMessage(string message)
        {
            if (string.IsNullOrEmpty(logFilePath)) return;
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:      - {message}";
            try
            {
                using (var writer = new StreamWriter(logFilePath, append: true))
                {
                    writer.WriteLine(line);
                }
                Console.WriteLine(line);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Logger AddMessage error: " + ex.Message);
            }
        }
    }
}

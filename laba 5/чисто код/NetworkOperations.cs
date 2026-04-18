using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace laba_5
{
    public static class NetworkOperations
    {
        public static string MyIP = "127.0.0.2"; //не использую
        public static int MyPort = 5001;

        public static string path = "storage";


        public static void Init(int port = 5001, string ip = "127.0.0.2")
        {
            MyIP = ip;
            MyPort = port;
            path = Path.Combine(Directory.GetCurrentDirectory(), path);
            Directory.CreateDirectory(path);
            Console.WriteLine($"Рабочая папка: {path}");
        }





        public static async Task StartListening()
        {
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            while(true)
            {
                try
                {
                    server.Bind(new IPEndPoint(IPAddress.Parse(MyIP), MyPort));
                    break;
                }
                catch
                {
                    MyPort++;
                    continue;
                }
            }

            
            
            server.Listen(10);

            Console.WriteLine($"\nПорт {MyPort} открыт\n");

            while (true)
            {
                Socket client = await server.AcceptAsync();
                var remoteEP = (IPEndPoint)client.RemoteEndPoint;
                string ip = remoteEP.Address.ToString();
                int port = remoteEP.Port;

                Task clientTask = Task.Run(async () =>
                {
                    try
                    {
                        await HandleClient(client);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Client error: {ex.Message}");
                    }
                    finally
                    {
                        try {
                            client.Shutdown(SocketShutdown.Both);
                        } catch { }
                        client.Close();
                    }
                });

                Console.Write($"\n\nПодключение {ip}:{port}\n\n");
            }
        }

        public static async Task HandleClient(Socket client)
        {
            byte[] buffer = new byte[4096];
            List<byte> data = new List<byte>();

            while (true)
            {
                int received = await client.ReceiveAsync(buffer);
                if (received == 0) break;

                // добавляем в общий буфер
                data.AddRange(buffer.Take(received));

                // пробуем понять, полный ли HTTP
                if (TryGetFullHttpMessage(data.ToArray(), out byte[] fullMessage))
                {
                    await ParseHTTP(fullMessage, client);
                    break;
                }
            }
        }
        static bool TryGetFullHttpMessage(byte[] data, out byte[] message)
        {
            message = null;

            string text = Encoding.UTF8.GetString(data);

            int headerEnd = text.IndexOf("\r\n\r\n");
            if (headerEnd == -1)
                return false; // заголовки ещё не пришли полностью

            int bodyStart = headerEnd + 4;

            // ищем Content-Length
            int contentLength = 0;

            var lines = text.Substring(0, headerEnd).Split("\r\n");
            foreach (var line in lines)
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    contentLength = int.Parse(line.Split(':')[1].Trim());
                }
            }

            int totalLength = bodyStart + contentLength;

            if (data.Length < totalLength)
                return false; // body ещё не весь пришёл

            message = data.Take(totalLength).ToArray();
            return true;
        }


        public static async Task ParseHTTP(byte[] message, Socket client)
        {
            string request = Encoding.UTF8.GetString(message);

            Console.WriteLine("\n\n----- HTTP REQUEST -----\n");
            Console.WriteLine(request);

            //Сама строка запроса
            string[] lines = request.Split("\r\n");
            string[] requestLine = lines[0].Split(' ');

            //заголовки для From/ to в PUT
            Dictionary<string, string> headers = new();
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) break;
                int sepIndex = lines[i].IndexOf(':');
                if (sepIndex > 0)
                {
                    string key = lines[i][..sepIndex].Trim();
                    string value = lines[i][(sepIndex + 1)..].Trim();

                    headers[key] = value;
                }
            }

            string method = requestLine[0];
            string url = requestLine[1];

            //Убираем абракадабры браузера
            url = WebUtility.UrlDecode(url);
            url = url.TrimStart('/');
            string fullPath = Path.Combine(path, url);

            Console.WriteLine($"Method: {method}");
            Console.WriteLine($"URL: {url}");

            // Тело (доверюсь GPT,потому что я не хочу пока в HTML лезть)
            string body = "";
            int emptyLineIndex = Array.IndexOf(lines, "");

            if (emptyLineIndex >= 0 && emptyLineIndex < lines.Length - 1)
            {
                body = string.Join("\r\n", lines[(emptyLineIndex + 1)..]);
            }

            Console.WriteLine($"Полный путь запроса: {fullPath}");

            string responseBody = "";
            string status = "200 OK";

            // Отдать файл
            if (method == "GET")
            {
                if (File.Exists(fullPath))
                {
                    responseBody = File.ReadAllText(fullPath);
                }
                else if (Directory.Exists(fullPath))
                {
                    var files = Directory.GetFileSystemEntries(fullPath)
                                         .Select(Path.GetFileName);

                    responseBody = string.Join("\n", files);
                }
                else
                {
                    status = "404 Not Found";
                    responseBody = "Not Found";
                }
            }

            //поместить файл
            else if (method == "PUT")
            {
                string copyFrom = null;

                if (headers != null && headers.TryGetValue("X-Copy-From", out var from))
                {
                    copyFrom = from;
                }

                if (!string.IsNullOrEmpty(copyFrom))
                {
                    string sourcePath = Path.Combine(path, copyFrom.TrimStart('/'));

                    if (!File.Exists(sourcePath))
                    {
                        status = "404 Not Found";
                        responseBody = "Source file not found";
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

                        File.Copy(sourcePath, fullPath, true);

                        status = "201 Created";
                        responseBody = "Copied";
                    }
                }
                else
                {

                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    File.WriteAllText(fullPath, body);
                    status = "201 Created";
                    responseBody = "Saved";
                }
            }
            //удалить файл
            else if (method == "DELETE")
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    responseBody = "Deleted";
                }
                else
                {
                    status = "404 Not Found";
                    responseBody = "Not Found";
                }
            }
            //чуть-чуть костылей (всё сразу в заголовок и отправить)
            else if (method == "HEAD")
            {
                if (File.Exists(fullPath))
                {
                    FileInfo info = new FileInfo(fullPath);

                    long size = info.Length;
                    string lastModified = info.LastWriteTimeUtc.ToString("R");

                    string response1 =
                        $"HTTP/1.1 200 OK\r\n" +
                        $"Content-Length: 0\r\n" +
                        $"File-Size: {size}\r\n" +
                        $"Last-Modified: {lastModified}\r\n\r\n";

                    Console.WriteLine("\n\n----- HEAD RESPONSE -----\n");
                    Console.WriteLine(response1);

                    await client.SendAsync(Encoding.UTF8.GetBytes(response1));
                    return;
                }
                else
                {
                    string response1 =
                        "HTTP/1.1 404 Not Found\r\n" +
                        "Content-Length: 0\r\n\r\n";

                    Console.WriteLine("\n\n----- HEAD RESPONSE -----\n");
                    Console.WriteLine(response1);

                    await client.SendAsync(Encoding.UTF8.GetBytes(response1));
                    return;
                }
            }
            //чё-то неизвестное
            else
            {
                status = "400 Bad Request";
                responseBody = "Unknown method";
            }

            // сам ответ
            string response =
                $"HTTP/1.1 {status}\r\n" +
                $"Content-Length: {Encoding.UTF8.GetByteCount(responseBody)}\r\n" +
                "Content-Type: text/plain\r\n\r\n" +
                responseBody;

            Console.WriteLine("\n\n----- RESPONSE -----\n");
            Console.WriteLine(response);

            await client.SendAsync(Encoding.UTF8.GetBytes(response));
        }


        //public static async Task ParseHTTP(byte[] message, Socket client)
        //{
        //    string request = Encoding.UTF8.GetString(message);

        //    Console.Write("\n\n----- HTTP REQUEST -----\n\n");
        //    Console.Write(request);

        //    string body = "<h1>request is accepted</h1>";
        //    string response =
        //        "HTTP/1.1 200 OK\r\n" + $"Content-Length: {body.Length}" +
        //        "\r\n\r\n" + body;

        //    Console.WriteLine("\n\nОтвет:" + response);

        //    await client.SendAsync(Encoding.UTF8.GetBytes(response));
        //}
    }
}

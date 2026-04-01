using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ProxyLaba
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Настройка сервера");
            Console.Write("\n\nВведите IP: ");
            string IP = "127.0.0.2";
            try
            {
                IP = Console.ReadLine();
                if (string.IsNullOrEmpty(IP)) IP = "127.0.0.2";
            }
            catch
            {
                IP = "127.0.0.2";
            }
            int port = 8888;
            Console.Write("\n\nВведите Порт: ");
            try
            {
                port = Convert.ToInt32(Console.ReadLine());
            }
            catch
            {
                port = 8888;

            }
            Console.WriteLine($"Установлено: {IP}:{port}");
            Logger.Init("note.txt");
            MyProxy proxyServ = new(IP, port);
            await proxyServ.StartAsync();




            //TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.2"), 8888);
            //listener.Start();

            //Console.WriteLine("Proxy started on 127.0.0.2:8888");

            //while (true)
            //{
            //    var client = listener.AcceptTcpClient();
            //    Task.Run(() => HandleClient(client));
            //}
        }
    }
}
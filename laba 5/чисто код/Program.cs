using laba_5;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        NetworkOperations.Init();
        Console.SetBufferSize(Console.BufferWidth, 10000);
        await NetworkOperations.StartListening();
    }
}

    
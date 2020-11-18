using System;
using System.Threading;
using System.Threading.Tasks;
using HttpServerLite;

namespace Test.Latency
{
    class Program
    {
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static Webserver _Server; 

        static void Main(string[] args)
        {
            StartServer();
            Console.WriteLine("Started on http://" + _Hostname + ":" + _Port);
            Console.WriteLine("CTRL-C to exit");
            Console.ReadLine();
        }

        static void StartServer()
        { 
            Console.WriteLine("Initializing server");
            _Server = new Webserver(_Hostname, _Port, DefaultRoute);
            _Server.Settings.Headers.Host = "https://" + _Hostname + ":" + _Port;
            _Server.Start(); 
        } 

        static async Task DefaultRoute(HttpContext ctx)
        {
            await ctx.Response.SendAsync(0);
        }
    }
}

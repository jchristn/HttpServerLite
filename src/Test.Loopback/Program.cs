using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HttpServerLite;

namespace Test.Loopback
{
    class Program
    {
        static string _Hostname = "localhost";
        static int _Port = 9090;
        static Webserver _Server;

        static void Main(string[] args)
        {
            _Server = new Webserver(_Hostname, _Port, false, null, null, DefaultRoute); 
            _Server.Start();
            Console.WriteLine("HttpServerLite listening on http://" + _Hostname + ":" + _Port);
            Console.WriteLine("ENTER to exit");
            Console.ReadLine();
        }
         
        static async Task DefaultRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;  
            await ctx.Response.SendAsync(0);
        }
    }
}

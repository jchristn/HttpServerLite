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
        static Webserver _Server;

        static void Main(string[] args)
        {
            _Server = new Webserver("localhost", 9000, false, null, null, DefaultRoute); 
            _Server.Start();
            Console.WriteLine("HttpServerLite listening on http://localhost:9000");
            Console.WriteLine("ENTER to exit");
            Console.ReadLine();
        }
         
        static async Task DefaultRoute(HttpContext ctx)
        {
            byte[] resp = new byte[0];
            ctx.Response.StatusCode = 200; 
            ctx.Response.ContentLength = resp.Length;
            await ctx.Response.SendAsync(resp);
        }
    }
}

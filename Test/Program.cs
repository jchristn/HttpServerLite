using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HttpServerLite;

namespace Test
{
    class Program
    {
        static Webserver _Server;
        static bool _Debug = true;

        static void Main(string[] args)
        {
            _Server = new Webserver("localhost", 9000, false, null, null, DefaultRoute);
            _Server.Start();
            Console.WriteLine("http://localhost:9000");
            Console.WriteLine("ENTER to exit");
            Console.ReadLine();
        }

        static void DefaultRoute(HttpContext ctx)
        {
            if (_Debug) Console.WriteLine(ctx.Request.ToString());

            byte[] resp = null;

            if (ctx.Request.RawUrlWithoutQuery.Equals("/html/index.html"))
            {
                resp = File.ReadAllBytes("./html/index.html");
                ctx.Response.ContentType = "text/html";
            }
            else if (ctx.Request.RawUrlWithoutQuery.Equals("/img/watson.jpg"))
            {
                resp = File.ReadAllBytes("./img/watson.jpg");
                ctx.Response.ContentType = "image/jpeg";
            }
            else
            {
                resp = Encoding.UTF8.GetBytes(ctx.Request.ToString());
            }

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentLength = resp.Length;
            ctx.Response.Send(resp);
        }
    }
}

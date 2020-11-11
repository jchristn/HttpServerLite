using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HttpServerLite; 

namespace Test
{
    class Program
    {
        static Webserver _Server;
        static bool _RunForever = true;
        static bool _Debug = false;
        static int _BufferSize = 65536;

        static void Main(string[] args)
        {
            StartServer();

            Console.WriteLine("Started on http://localhost:9000");

            while (_RunForever)
            {
                Console.Write("Command [? for help]: ");
                string userInput = Console.ReadLine();
                if (String.IsNullOrEmpty(userInput)) continue;

                switch (userInput)
                {
                    case "?":
                        Menu();
                        break;
                    case "q":
                        _RunForever = false;
                        break;
                    case "c":
                    case "cls":
                        Console.Clear();
                        break;
                    case "state":
                        Console.WriteLine(_Server.IsListening);
                        break;
                    case "start":
                        _Server.Start();
                        break;
                    case "stop":
                        _Server.Stop();
                        break;
                    case "dispose":
                        _Server.Dispose();
                        break; 
                    case "conn":
                        ListConnections();
                        break;
                }
            }
        }

        static void Menu()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine(" ?        help, this menu");
            Console.WriteLine(" q        quit");
            Console.WriteLine(" cls      clear the screen");
            Console.WriteLine(" state    display whether or not new connections are accepted");
            Console.WriteLine(" start    start accepting new connections");
            Console.WriteLine(" stop     stop accepting new connections");
            Console.WriteLine(" dispose  dispose of the server"); 
            Console.WriteLine(" conn     list connections");
            Console.WriteLine("");
        }

        static void StartServer()
        {
            if (_Server != null && _Server.IsListening)
            {
                Console.WriteLine("Already initialized");
                return;
            }
            else
            {
                Console.WriteLine("Initializing server");
                _Server = new Webserver("localhost", 9000, false, null, null, DefaultRoute);
                _Server.LoadRoutes();
                _Server.DefaultHeaders.Host = "http://localhost:9000";
                _Server.Events.ServerStarted = ServerStarted;
                _Server.Events.ServerStopped = ServerStopped;
                _Server.Events.ServerDisposed = ServerDisposed;
                _Server.Start();
            }
        }

        static void ListConnections()
        {
            IEnumerable<string> conns = _Server.Connections;
            Console.WriteLine("Connections:");

            if (conns != null && conns.Count() > 0)
            { 
                foreach (string conn in conns) Console.WriteLine("  " + conn);
            }
            else
            {
                Console.WriteLine("  (none)");
            }
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            if (_Debug) Console.WriteLine(ctx.Request.ToString());

            try
            {
                byte[] reqData = ctx.Request.Data;

                if (ctx.Request.RawUrlWithoutQuery.Equals("/"))
                {
                    string resp = "Hello from HttpServerLite";
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/html";
                    ctx.Response.ContentLength = resp.Length;
                    await ctx.Response.SendAsync(resp);
                    return;
                }
                else if (ctx.Request.RawUrlWithoutQuery.Equals("/wait"))
                {
                    Task.Delay(10000).Wait();
                    string resp = "Hello from HttpServerLite";
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/html";
                    ctx.Response.ContentLength = resp.Length;
                    await ctx.Response.SendAsync(resp);
                    return;
                }
                else if (ctx.Request.RawUrlWithoutQuery.Equals("/favicon.ico"))
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/html";
                    await ctx.Response.SendAsync(0);
                    return;
                }
                else if (ctx.Request.RawUrlWithoutQuery.Equals("/html/index.html"))
                {
                    SendFile(ctx, "./html/index.html", "text/html", _BufferSize);
                    return;
                }
                else if (ctx.Request.RawUrlWithoutQuery.Equals("/img/watson.jpg"))
                {
                    SendFile(ctx, "./img/watson.jpg", "image/jpg", _BufferSize);
                    return;
                }
                else if (ctx.Request.RawUrlWithoutQuery.Equals("/img-streamed/watson.jpg"))
                {
                    byte[] buffer = new byte[8192];
                    long len = new FileInfo("./img/watson.jpg").Length;
                    long bytesRemaining = len;

                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "image/jpeg";
                    ctx.Response.ContentLength = len;

                    using (FileStream fs = new FileStream("./img/watson.jpg", FileMode.Open))
                    {
                        while (bytesRemaining > 0)
                        {
                            int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                bytesRemaining -= bytesRead;
                                if (bytesRead == buffer.Length)
                                {
                                    await ctx.Response.SendWithoutCloseAsync(buffer);
                                }
                                else
                                {
                                    byte[] tempBuffer = new byte[bytesRead];
                                    Buffer.BlockCopy(buffer, 0, tempBuffer, 0, bytesRead);
                                    await ctx.Response.SendWithoutCloseAsync(tempBuffer);
                                }

                                Thread.Sleep(100);
                            }
                        }

                        ctx.Response.Close();
                        return;
                    }
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    ctx.Response.ContentType = "text/plain";
                    ctx.Response.Send(true);
                    return;
                }
            }
            catch (Exception e)
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = "text/plain";
                ctx.Response.Send(e.ToString());
                Console.WriteLine(e.ToString());
                return;
            }
        }

        static void ServerStarted() => Console.WriteLine("Server started");
        static void ServerStopped() => Console.WriteLine("Server stopped");
        static void ServerDisposed() => Console.WriteLine("Server disposed");

        static void SendFile(HttpContext ctx, string file, string contentType, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            long contentLen = new FileInfo(file).Length; 

            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                ctx.Response.ContentType = contentType;
                ctx.Response.ContentLength = contentLen;
                ctx.Response.StatusCode = 200;
                ctx.Response.Send(contentLen, fs);
                return;
            }
        }

        [StaticRoute(HttpMethod.GET, "/static")]
        public static async Task MyStaticRoute(HttpContext ctx)
        {
            string resp = "Hello from the static route";
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength = resp.Length;
            await ctx.Response.SendAsync(resp);
            return;
        }

        [DynamicRoute(HttpMethod.GET, "^/dynamic/\\d+$")]
        public static async Task MyDynamicRoute(HttpContext ctx)
        {
            string resp = "Hello from the dynamic route";
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength = resp.Length;
            await ctx.Response.SendAsync(resp);
            return;
        }
    }
}

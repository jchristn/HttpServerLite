using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CavemanTcp;
using HttpServerLite; 

namespace Test
{
    class Program
    {
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static Webserver _Server;
        static bool _RunForever = true;
        static bool _Debug = false;
        static int _BufferSize = 65536;
        static bool _UseStream = true;

        static void Main(string[] args)
        {
            StartServer();

            Console.WriteLine("Started on http://" + _Hostname + ":" + _Port);

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
                _Server = new Webserver(_Hostname, _Port, false, null, null, DefaultRoute);
                _Server.Settings.Headers.Host = "http://" + _Hostname + ":" + _Port;
                _Server.Events.ServerStarted += ServerStarted;
                _Server.Events.ServerStopped += ServerStopped;
                _Server.Events.ServerDisposing += ServerDisposing;
                _Server.Events.Logger = Console.WriteLine;
                _Server.Routes.Content.Add("./html/", true);
                _Server.Routes.Content.Add("./img/", true);
                _Server.Routes.PostRouting = PostRoutingHandler;
                _Server.Settings.Debug.Responses = true;
                _Server.Settings.Debug.Routing = true;
                _Server.Start();
            }
        }

        static void ListConnections()
        {
            IEnumerable<ClientMetadata> conns = _Server.Connections;
            Console.WriteLine("Connections:");

            if (conns != null && conns.Count() > 0)
            { 
                foreach (ClientMetadata conn in conns) Console.WriteLine("  " + conn.IpPort);
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
                byte[] data = null;

                if (_UseStream)
                {
                    if (ctx.Request.ContentLength > 0)
                    {
                        long bytesRemaining = ctx.Request.ContentLength;
                        int read = 0;
                        byte[] buffer = null;

                        using (MemoryStream ms = new MemoryStream())
                        {
                            while (bytesRemaining > 0)
                            {
                                if (bytesRemaining > 4096) buffer = new byte[4096];
                                else buffer = new byte[(int)bytesRemaining];

                                read = ctx.Request.Data.Read(buffer, 0, buffer.Length);
                                if (read > 0)
                                {
                                    ms.Write(buffer, 0, read);
                                    bytesRemaining -= read;
                                }
                            }

                            ms.Seek(0, SeekOrigin.Begin);
                            data = ms.ToArray();
                        }

                        Console.WriteLine("Read " + ctx.Request.ContentLength + " bytes from stream: " + Encoding.UTF8.GetString(data));
                    }
                }
                else
                {
                    if (ctx.Request.ContentLength > 0)
                    {
                        data = ctx.Request.DataAsBytes;
                        Console.WriteLine("Read " + ctx.Request.ContentLength + " bytes from request: " + Encoding.UTF8.GetString(data));
                    }
                }

                if (ctx.Request.Url.WithoutQuery.Equals("/"))
                {
                    string resp = DefaultHtml();
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/html";
                    await ctx.Response.SendAsync(resp);
                    return;
                }
                else if (ctx.Request.Url.WithoutQuery.Equals("/wait"))
                {
                    Task.Delay(10000).Wait();
                    string resp = "Hello from HttpServerLite";
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/html";
                    await ctx.Response.SendAsync(resp);
                    return;
                }
                else if (ctx.Request.Url.WithoutQuery.Equals("/favicon.ico"))
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/html";
                    await ctx.Response.SendAsync(0);
                    return;
                }
                else if (ctx.Request.Url.WithoutQuery.Equals("/html/index.html"))
                {
                    SendFile(ctx, "./html/index.html", "text/html", _BufferSize);
                    return;
                }
                else if (ctx.Request.Url.WithoutQuery.Equals("/img/watson.jpg"))
                {
                    SendFile(ctx, "./img/watson.jpg", "image/jpg", _BufferSize);
                    return;
                }
                else if (ctx.Request.Url.WithoutQuery.Equals("/img-streamed/watson.jpg"))
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

        static void ServerStarted(object sender, EventArgs args) => Console.WriteLine("Server started");

        static void ServerStopped(object sender, EventArgs args) => Console.WriteLine("Server stopped");

        static void ServerDisposing(object sender, EventArgs args) => Console.WriteLine("Server disposing");

        static void SendFile(HttpContext ctx, string file, string contentType, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            long contentLen = new FileInfo(file).Length; 

            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                ctx.Response.ContentType = contentType; 
                ctx.Response.StatusCode = 200;
                ctx.Response.Send(contentLen, fs);
                return;
            }
        }

        static string DefaultHtml()
        {
            return
                "<html>" +
                " <head><title>HttpServerLite</title></head>" +
                " <body><h2>HttpServerLite</h2><p>HttpServerLite is running!</p></body>" +
                "</html>";
        }

        [StaticRoute(HttpMethod.GET, "/static")]
        public static async Task MyStaticRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.SendAsync("Hello from the static route");
            return;
        }

        [StaticRoute(HttpMethod.GET, "/static/1")]
        public static async Task MyStatic1Route(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.SendAsync("Hello from the static 1 route");
            return;
        }

        [StaticRoute(HttpMethod.GET, "/static/2")]
        public static async Task MyStatic2Route(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.SendAsync("Hello from the static 2 route");
            return;
        }

        [StaticRoute(HttpMethod.GET, "/mirror")]
        public static async Task MyMirrorRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.SendAsync(ctx.ToJson(true));
            return;
        }

        [StaticRoute(HttpMethod.GET, "/mirror/1")]
        public static async Task MyMirror1Route(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.SendAsync(ctx.ToJson(true));
            return;
        }

        [StaticRoute(HttpMethod.GET, "/mirror/2")]
        public static async Task MyMirror2Route(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.SendAsync(ctx.ToJson(true));
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/{version}/api/{id}")]
        public static async Task MyParameterRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.SendAsync("Hello from the parameter route version " + ctx.Request.Url.Parameters["version"] + " ID " + ctx.Request.Url.Parameters["id"]);
            return;
        }

        [DynamicRoute(HttpMethod.GET, "^/dynamic/\\d+$")]
        public static async Task MyDynamicRoute(HttpContext ctx)
        {
            string resp = "Hello from the dynamic route";
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain"; 
            await ctx.Response.SendAsync(resp);
            return;
        }

        private static async Task PostRoutingHandler(HttpContext ctx)
        {
            Console.WriteLine(ctx.Request.Method.ToString() + " " + ctx.Request.Url.WithoutQuery + ": " + ctx.Response.StatusCode + " (" + ctx.Timestamp.TotalMs + ")");
        }
    }
}

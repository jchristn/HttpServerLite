using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HttpServerLite;
using HttpServerLite.Routes;

namespace Test
{
    class Program
    {
        static Webserver _Server;
        static bool _Debug = false;

        static void Main(string[] args)
        {
            _Server = new Webserver("localhost", 9000, false, null, null, DefaultRoute)
                .LoadRoutes();
            _Server.DefaultHeaders.Host = "http://localhost:9000";
            // _Server.Events.ConnectionReceived = ConnectionReceived;
            _Server.Start();
            Console.WriteLine("http://localhost:9000");
            Console.WriteLine("ENTER to exit");
            Console.ReadLine();
        }

        static void ConnectionReceived(string ip, int port)
        {
            Console.WriteLine("Connection received from " + ip + ":" + port);
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            if (_Debug) Console.WriteLine(ctx.Request.ToString());

            byte[] reqData = ctx.Request.Data;
            byte[] resp = null;

            if (ctx.Request.RawUrlWithoutQuery.Equals("/"))
            { 
                resp = Encoding.UTF8.GetBytes("Hello from HttpServerLite\r\n");
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/html";
            }
            else if (ctx.Request.RawUrlWithoutQuery.Equals("/favicon.ico"))
            { 
                resp = new byte[0];
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/html";
            } 
            else if (ctx.Request.RawUrlWithoutQuery.Equals("/html/index.html"))
            { 
                resp = await File.ReadAllBytesAsync("./html/index.html");
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/html";
            }
            else if (ctx.Request.RawUrlWithoutQuery.Equals("/img/watson.jpg"))
            { 
                resp = await File.ReadAllBytesAsync("./img/watson.jpg");
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "image/jpeg";
            }
            else if (ctx.Request.RawUrlWithoutQuery.Equals("/img-streamed/watson.jpg"))
            {
                Console.WriteLine("Watson streamed route");
                byte[] buffer = new byte[8192];
                long len = new FileInfo("./img/watson.jpg").Length;
                long bytesRemaining = len;

                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "image/jpeg";

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
            }

            ctx.Response.ContentLength = resp.Length;
            await ctx.Response.SendAsync(resp);
        }

        [Route("Test")]
        public async Task TestRoute(HttpContext context)
        {
            byte[] response = Encoding.UTF8.GetBytes("HttpServerLite test route");
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html";
            await context.Response.SendAsync(response);
        }
    }
}

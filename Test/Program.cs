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

        static async Task DefaultRoute(HttpContext ctx)
        {
            if (_Debug) Console.WriteLine(ctx.Request.ToString());

            byte[] resp = null;

            if (ctx.Request.RawUrlWithoutQuery.Equals("/html/index.html"))
            {
                resp = await File.ReadAllBytesAsync("./html/index.html");
                ctx.Response.ContentType = "text/html";
            }
            else if (ctx.Request.RawUrlWithoutQuery.Equals("/img/watson.jpg"))
            {
                resp = await File.ReadAllBytesAsync("./img/watson.jpg");
                ctx.Response.ContentType = "image/jpeg";
            }
            else if (ctx.Request.RawUrlWithoutQuery.Equals("/img-streamed/watson.jpg"))
            {
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
                resp = Encoding.UTF8.GetBytes(ctx.Request.ToString());
            }
             
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentLength = resp.Length; 
            await ctx.Response.SendAsync(resp); 
        }
    }
}

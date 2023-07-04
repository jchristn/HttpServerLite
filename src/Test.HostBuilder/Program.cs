using System;
using System.Threading.Tasks;
using HttpServerLite;

namespace Test.HostBuilder
{
    public static class Program
    {
        private static string _Hostname = "localhost";
        private static int _Port = 8000;
        private static bool _Ssl = false;

        public static void Main(string[] args)
        {
            Console.WriteLine("Staring webserver on " + (_Ssl ? "https://" : "http://") + _Hostname + ":" + _Port);

            Webserver server = new HttpServerLite.Extensions.HostBuilderExtension.HostBuilder(_Hostname, _Port, DefaultRoute)
                .MapStaticRoute(HttpMethod.GET, Route1, $"/{nameof(Route1)}")
                .MapStaticRoute(HttpMethod.GET, Route2, $"/{nameof(Route2)}")
                .Build(); 

            server.Start();
            Console.WriteLine("Webserver started, press ENTER to exit");
            Console.ReadLine();
        }
        static async Task DefaultRoute(HttpContext ctx)
            => await ctx.Response.SendAsync("Hello from the default route!");

        static async Task Route1(HttpContext ctx)
            => await ctx.Response.SendAsync($"Hello from {nameof(Route1)}!");

        static async Task Route2(HttpContext ctx)
            => await ctx.Response.SendAsync($"Hello from {nameof(Route2)}!");
    }
}

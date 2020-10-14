using System;
using System.Threading.Tasks;
using HttpServerLite;

namespace Test.Routes
{
    class Program
    {
        static string _Hostname = "127.0.0.1";
        static int _Port = 8080; 

        static void Main(string[] args)
        {
            using (var server = new Webserver(_Hostname, _Port, DefaultRoute).LoadRoutes())
            {
                server.Start();

                Console.WriteLine("Listening on http://" + _Hostname + ":" + _Port);
                Console.WriteLine("Press ENTER to exit");
                Console.ReadLine();
            }
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            await ctx.Response.SendAsync("Default route");
        }

        [StaticRoute(HttpMethod.GET, "hello")]
        public async Task HelloRoute(HttpContext ctx)
        {
            await ctx.Response.SendAsync("Static route GET /hello");
        }

        [StaticRoute(HttpMethod.POST, "submit")]
        public async Task PostRoute(HttpContext ctx)
        {
            await ctx.Response.SendAsync("Static route POST /submit");
        }

        [DynamicRoute(HttpMethod.PUT, "^/foo/")]
        public async Task PutRouteWithoutId(HttpContext ctx)
        {
            await ctx.Response.SendAsync("Dynamic route PUT /foo/");
        }

        [DynamicRoute(HttpMethod.DELETE, "^/foo/\\d+$")]
        public async Task PutRouteWithId(HttpContext ctx)
        {
            await ctx.Response.SendAsync("Dynamic route DELETE /foo/[id]");
        }
    }
}

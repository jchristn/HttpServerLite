using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HttpServerLite.Routes;
using NUnit.Framework;

namespace HttpServerLite.Test.Routes
{
    public class RouteDetectionTest
    {
        private const string ExpectedDefaultRouteOutput = "DefaultRoute";
        private const string ExpectedTestRouteOutput = "TestRoute";
        private const string ExpectedStaticTestRouteOutput = "StaticTestRoute";
        private ushort port;

        [SetUp]
        public void StartServer()
        {
            port = TestHelper.GetPort();
            
            var server = new Webserver("localhost", port, false, null, null, DefaultRoute)
                .LoadRoutes();
            server.DefaultHeaders.Host = $"http://localhost:{port}";
            server.Start(); 
        }
        
        /*
         * Routes
         */
        
        static async Task DefaultRoute(HttpContext context)
        {
            byte[] response = Encoding.UTF8.GetBytes(ExpectedDefaultRouteOutput);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html";
            await context.Response.SendAsync(response); 
        }

        [Route("Test")]
        public async Task TestRoute(HttpContext context)
        {
            byte[] response = Encoding.UTF8.GetBytes(ExpectedTestRouteOutput);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html";
            await context.Response.SendAsync(response);
        }
        
        [Route("StaticTest")]
        public static async Task StaticTestRoute(HttpContext context)
        {
            byte[] response = Encoding.UTF8.GetBytes(ExpectedStaticTestRouteOutput);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html";
            await context.Response.SendAsync(response);
        }
        
        /*
         * Tests
         */
        
        [Test]
        public async Task DefaultRouteTest()
        {
            var serverAddress = $"http://localhost:{port}";
            using var client = new HttpClient();

            var defaultRouteOutput = await client.GetStringAsync($"{serverAddress}/index");
            Assert.AreEqual(ExpectedDefaultRouteOutput, defaultRouteOutput);
        }
        
        [Test]
        public async Task TestRouteTest()
        {
            var serverAddress = $"http://localhost:{port}";
            using var client = new HttpClient();

            var testRouteOutput = await client.GetStringAsync($"{serverAddress}/test");
            Assert.AreEqual(ExpectedTestRouteOutput, testRouteOutput);
        }
        
        [Test]
        public async Task StaticTestRouteTest()
        {
            var serverAddress = $"http://localhost:{port}";
            using var client = new HttpClient();

            var staticTestRouteOutput = await client.GetStringAsync($"{serverAddress}/statictest");
            Assert.AreEqual(ExpectedStaticTestRouteOutput, staticTestRouteOutput);
        }
    }
}
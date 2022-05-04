using System;
using System.Threading.Tasks;
using HttpServerLite;

namespace Test.ConnectionAuthorization
{
    internal class Program
    {
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static Webserver _Server;
        static int _Counter = 0;

        static void Main(string[] args)
        {
            _Server = new Webserver(_Hostname, _Port, false, null, null, DefaultRoute);
            _Server.Settings.Headers.Host = "http://" + _Hostname + ":" + _Port;
            _Server.Callbacks.AuthorizeConnection = AuthorizeConnection;
            _Server.Events.Logger = Console.WriteLine;
            _Server.Routes.Content.Add("./html/", true);
            _Server.Routes.Content.Add("./img/", true);
            _Server.Settings.Debug.Responses = true;
            _Server.Settings.Debug.Routing = true;
            _Server.Start();

            Console.WriteLine("");
            Console.WriteLine("Server started on http://" + _Hostname + ":" + _Port);
            Console.WriteLine("Every other connection will be denied");
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }

        private static bool AuthorizeConnection(string arg1, int arg2)
        {
            // deny every other connection
            _Counter += 1;
            if (_Counter % 2 == 0)
            {
                Console.WriteLine("Declined: " + arg1 + ":" + arg2);
                return false;
            }
            return true;
        }

        private static async Task DefaultRoute(HttpContext arg)
        {
            Console.WriteLine(arg.Request.Source.IpAddress + ":" + arg.Request.Source.Port + ": " + arg.Request.Method.ToString() + " " + arg.Request.Url.Full);
            arg.Response.StatusCode = 200;
            arg.Response.ContentType = "text/plain";
            await arg.Response.SendAsync("You made it!" + Environment.NewLine);
        }
    }
}

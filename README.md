![alt tag](https://github.com/jchristn/httpserverlite/blob/master/assets/icon.ico)

# HttpServerLite

TCP-based user-space HTTP and HTTPS server, written in C#.

## New in v1.0.0

- Alpha

## Special Thanks

I'd like to extend a special thanks to those that have provided motivation or otherwise directly helped make HttpServerLite better.

- @winkmichael

## Performance

HttpServerLite is quite fast, however, it's in user-space and is much slower than other webservers that have the benefit of a kernel-mode driver (such as ```http.sys``` and IIS or Watson).  Simple tests using Bombardier (https://github.com/codesenberg/bombardier) show Watson Webserver (see https://github.com/jchristn/watsonwebserver) to be roughly 3X-5X lower latency and 3X-5X higher throughput.

Therefore, use of HttpServerLite is primarily recommended for those that need to satisfy specific use cases where the utmost control over HTTP is required and simply not allowed by webservers that are built on top of ```http.sys```.

## Test App

Refer to the ```Test``` project for a working example.

### Simple Server
```
using System;
using System.Threading.Tasks;
using HttpServerLite;

namespace Test
{
  class Program
  {
    static Webserver _Server;

    static void Main(string[] args)
    {
      Webserver server = new Webserver("localhost", 9000, false, null, null, DefaultRoute); 
      server.Start();
      Console.WriteLine("HttpServerLite listening on http://localhost:9000");
      Console.WriteLine("ENTER to exit");
      Console.ReadLine();
    }
         
    static async Task DefaultRoute(HttpContext ctx)
    {
      string resp = "Hello from HttpServerLite!";
      ctx.Response.StatusCode = 200; 
      ctx.Response.ContentLength = resp.Length;
      await ctx.Response.SendAsync(resp);
    }
  }
} 
```

## Routing

Placeholder, to be added.

## Accessing from Outside Localhost

When you configure HttpServerLite to listen on ```127.0.0.1``` or ```localhost```, it will only respond to requests received from within the local machine.

To configure access from other nodes outside of ```localhost```, use the following:

- Specify the exact DNS hostname upon which HttpServerLite should listen in the Server constructor. The HOST header on incoming HTTP requests MUST match this value (this is an operating system limitation)
- If you want to listen on more than one hostname or IP address, use ```*``` or ```+```. You MUST run HttpServerLite as administrator for this to work (this is an operating system limitation)
- If you want to use a port number less than 1024, you MUST run HttpServerLite as administrator (this is an operating system limitation)
- Open a port on your firewall to permit traffic on the TCP port upon which HttpServerLite is listening
- Since HttpServerLite is not based on ```http.sys``` **you should not have to add URL ACLs** 
- If you're still having problems, please do not hesitate to file an issue here, and I will do my best to help and update the documentation.

## Version History

Refer to CHANGELOG.md for version history.

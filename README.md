![alt tag](https://raw.githubusercontent.com/jchristn/HttpServerLite/master/Assets/icon.ico)

# HttpServerLite

[![NuGet Version](https://img.shields.io/nuget/v/HttpServerLite.svg?style=flat)](https://www.nuget.org/packages/HttpServerLite/) [![NuGet](https://img.shields.io/nuget/dt/HttpServerLite.svg)](https://www.nuget.org/packages/HttpServerLite) 

TCP-based user-space HTTP and HTTPS server, written in C#.

## New in v1.0.4

- Dependency update
- Configurable match preference in dynamic routes manager

## Special Thanks

I'd like to extend a special thanks to those that have provided motivation or otherwise directly helped make HttpServerLite better.

- @winkmichael
- @Job79

## Performance

HttpServerLite is quite fast, however, it's in user-space and is much slower than other webservers that have the benefit of a kernel-mode driver (such as ```http.sys``` and IIS or Watson).  Simple tests using Bombardier (https://github.com/codesenberg/bombardier) show Watson Webserver (see https://github.com/jchristn/watsonwebserver) to be roughly 3X-5X lower latency and 3X-5X higher throughput.

Therefore, use of HttpServerLite is primarily recommended for those that need to satisfy specific use cases where the utmost control over HTTP is required and simply not allowed by webservers that are built on top of ```http.sys```.

## Getting Started

Refer to the ```Test``` project for a working example.

It is important to under that that HttpServerLite is minimalistic and leaves control to you on which headers are set.  Thus it is important to understand the following:

- ```server.DefaultHeaders``` contains default values for a series of HTTP headers
  - These will be included **in every response** if they have a value assigned
  - The values in ```server.DefaultHeaders``` can be written directly, or
    - You can modify per-response values by using ```ctx.Response.Headers.Add("[header]", "[value]")```
    - Values set in ```ctx.Response.Headers``` will override any value in ```server.DefaultHeaders``` for that response only
  - The headers automatically set if a value is supplied include
    - Access-Control-Allow-[Origin|Methods|Headers]
    - Access-Control-Expose-Headers
    - Accept
    - Accept-[Language|Charset]
    - Connection
    - Host
  - ```Connection``` is an example of one of these headers.  By default it is set to ```close```, therefore you should:
    - Leave it as is
    - Explicitly set it prior to sending a response using ```ctx.Response.Headers.Add("connection", "value")```, or
    - Set the default value in ```server.DefaultHeaders.Connection```
- ```ctx.Response.ContentLength``` should be set if you want the ```Content-Length``` header to be sent
- ```server.DefaultHeaders.Host``` should be set when instantiating the server though it is not required

### Simple Server
```csharp
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
      server.DefaultHeaders.Host = "https://localhost:9000";
      server.DefaultHeaders.Connection = "close";
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
      ctx.Response.ContentType = "text/plain";
      await ctx.Response.SendAsync(resp);
    }
  }
} 
```

## Routing

HttpServerLite includes the following routing capabilities.  These are listed in the other in which they are processed within HttpServerLite:

- ```server.AccessControl``` - access control based on IP address
  - You can specify the ```Mode``` to either be ```DefaultPermit``` or ```DefaultDeny```
    - ```DefaultPermit``` will allow everything unless explicitly blocked through ```DenyList```
    - ```DefaultDeny``` will deny everything unless explicitly permitted through ```PermitList```
    - The default value is ```DefaultPermit```
- ```server.OptionsRoute``` - a default route to use when the HTTP verb is ```OPTIONS```
  - When set, the connection is terminated after being handled by ```server.OptionsRoute```
- ```server.PreRoutingHandler``` - a route through which **all** requests will pass, useful for authentication, logging, and other functions
  - If defined, return ```true``` from this task if you wish to terminate the connection
  - Otherwise return ```false``` to allow routing to continue
- ```server.ContentRoutes``` - serve GET and HEAD requests for static content based on URL path
  - Content will be read from the ```server.ContentRoutes.BaseDirectory``` plus the URL path
  - An entire directory can be listed as a content route when adding the route
- ```server.StaticRoutes``` - invoke functions based on specific HTTP method and URL combinations
- ```server.DynamicRoutes``` - invoke functions based on specific HTTP method and a regular expression for the URL
- ```server.DefaultRoute``` - any request that did not match a content route, static route, or dynamic route, is routed here

## Accessing from Outside Localhost

When you configure HttpServerLite to listen on ```127.0.0.1``` or ```localhost```, it will only respond to requests received from within the local machine.

To configure access from other nodes outside of ```localhost```, use the following:

- Specify the IP address on which HttpServerLite should listen in the Server constructor. 
- If you want to listen on more than one IP address, use ```*``` or ```+```
- If you listen on anything other than ```127.0.0.1```, you may have to run HttpServerLite as administrator (operating system dependent)
- If you want to use a port number less than 1024, you MUST run HttpServerLite as administrator (this is an operating system limitation)
- Open a port on your firewall to permit traffic on the TCP port upon which HttpServerLite is listening
- Since HttpServerLite is not based on ```http.sys``` **you should not have to add URL ACLs** 
- If you're still having problems, please do not hesitate to file an issue here, and I will do my best to help and update the documentation.

## Version History

Refer to CHANGELOG.md for version history.

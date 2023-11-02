# OBSOLETE

HttpServerLite has been merged with the .NET Foundation project [WatsonWebserver](https://github.com/dotnet/WatsonWebserver) as a subproject called ```Watson.Lite```.  

This repository has been moved to public archive as a result.  

We are thankful to the community that has contributed to this work and welcome you to continue efforts on the new repository!




![alt tag](https://raw.githubusercontent.com/jchristn/HttpServerLite/master/Assets/icon.ico)

# HttpServerLite

[![NuGet Version](https://img.shields.io/nuget/v/HttpServerLite.svg?style=flat)](https://www.nuget.org/packages/HttpServerLite/) [![NuGet](https://img.shields.io/nuget/dt/HttpServerLite.svg)](https://www.nuget.org/packages/HttpServerLite) 

TCP-based user-space HTTP and HTTPS server, written in C#, with no dependency on http.sys.

## New in v2.1.x

- ```HostBuilder``` feature to quickly build servers, thank you @sapurtcomputer30!
- Bugfix for ```HttpContext.HttpRequest.Data``` not ending, thank you @ChZhongPengCheng33

## Special Thanks

I'd like to extend a special thanks to those that have provided motivation or otherwise directly helped make HttpServerLite better.

- @winkmichael @Job79 @MartyIX @sqlnew @SaintedPsycho @Return25 @marcussacana @samisil 
- @Jump-Suit @sapurtcomputer30 @ChZhongPengCheng33 @bobaoapae

## Performance

HttpServerLite is quite fast, however, it's in user-space and may be slower than other webservers that have the benefit of a kernel-mode driver (such as ```http.sys``` and IIS or Watson). 

## Getting Started

Refer to the ```Test``` project for a working example.

It is important to under that that HttpServerLite is minimalistic and leaves control to you on which headers are set.  Thus it is important to understand the following:

- ```server.Settings.Headers``` contains default values for a series of HTTP headers
  - These will be included **in every response** if they have a value assigned
  - The values in ```server.Settings.Headers``` can be written directly, or
    - You can modify per-response values by using ```ctx.Response.Headers.Add("[header]", "[value]")```
    - Values set in ```ctx.Response.Headers``` will override any value in ```server.Settings.Headers``` for that response only
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
    - Set the default value in ```server.Settings.Headers.Connection```
- ```ctx.Response.ContentLength``` should be set if you want the ```Content-Length``` header to be sent
- ```server.Settings.Headers.Host``` should be set when instantiating the server though it is not required

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
      server.Settings.Headers.Host = "https://localhost:9000";
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

- ```server.Settings.AccessControl``` - access control based on IP address
  - You can specify the ```Mode``` to either be ```DefaultPermit``` or ```DefaultDeny```
    - ```DefaultPermit``` will allow everything unless explicitly blocked through ```DenyList```
    - ```DefaultDeny``` will deny everything unless explicitly permitted through ```PermitList```
    - The default value is ```DefaultPermit```
- ```server.Routes.Preflight``` - a default route to use when the HTTP verb is ```OPTIONS```
  - When set, the connection is terminated after being handled by ```server.OptionsRoute```
- ```server.Routes.PreRouting``` - a route through which **all** requests will pass, useful for authentication, logging, and other functions
  - If defined, return ```true``` from this task if you wish to terminate the connection
  - Otherwise return ```false``` to allow routing to continue
- ```server.Routes.Content``` - serve GET and HEAD requests for static content based on URL path
  - Content will be read from the ```server.Routes.Content.BaseDirectory``` plus the URL path
  - An entire directory can be listed as a content route when adding the route
- ```server.Routes.Static``` - invoke functions based on specific HTTP method and URL combinations
- ```server.Routes.Parameter``` - invoke functions based on specific HTTP method and URLs with embedded parameters.  These values are returned in ```HttpContext.HttpRequest.Url.Parameters```
- ```server.Routes.Dynamic``` - invoke functions based on specific HTTP method and a regular expression for the URL
- ```server.Routes.Default``` - any request that did not match a content route, static route, or dynamic route, is routed here

Additionally, you can annotate your own methods using the ```StaticRoute```, ```ParameterRoute```, or ```DynamicRoute``` attributes.  Methods decorated with these attributes must be marked as ```public```.

```csharp
Webserver server = new Webserver("localhost", 9000, false, null, null, DefaultRoute);
server.Start();

[StaticRoute(HttpMethod.GET, "/static")]
public static async Task MyStaticRoute(HttpContext ctx)
{
  string resp = "Hello from the static route";
  ctx.Response.StatusCode = 200;
  ctx.Response.ContentType = "text/plain";
  ctx.Response.ContentLength = resp.Length;
  await ctx.Response.SendAsync(resp);
  return;
}

[ParameterRoute(HttpMethod.GET, "/{version}/api/{id}")]
public static async Task MyParameterRoute(HttpContext ctx)
{
  string resp = "Hello from parameter route version " + ctx.Request.Url.Parameters["version"] + " for ID " + ctx.Request.Url.Parameters["id"];
  ctx.Response.StatusCode = 200;
  ctx.Response.ContentType = "text/plain";
  ctx.Response.ContentLength = resp.Length;
  await ctx.Response.SendAsync(resp);
  return;
}

[DynamicRoute(HttpMethod.GET, "^/dynamic/\\d+$")]
public static async Task MyDynamicRoute(HttpContext ctx)
{
  string resp = "Hello from the dynamic route";
  ctx.Response.StatusCode = 200;
  ctx.Response.ContentType = "text/plain";
  ctx.Response.ContentLength = resp.Length;
  await ctx.Response.SendAsync(resp);
  return;
}
```

## Authorizing or Declining a Connection
```
server.Callbacks.AuthorizeConnection = AuthorizeConnection;

private static bool AuthorizeConnection(string ipAddress, int port)
{
  // evaluate the IP address and port
  return true;  // permit
  return false; // deny
}
```

## HostBuilder

```HostBuilder``` helps you set up your server much more easily by introducing a chain of settings and routes instead of using the server class directly.

```csharp
using WatsonWebserver.Extensions.HostBuilderExtension;

Server server = new HostBuilder("127.0.0.1", 8000, false, DefaultRoute)
                .MapStaticRoute(WatsonWebserver.HttpMethod.GET, GetUrlsRoute, "/links")
                .MapStaticRoute(WatsonWebserver.HttpMethod.POST, CheckLoginRoute, "/login")
                .MapStaticRoute(WatsonWebserver.HttpMethod.POST, TestRoute, "/test")
                .Build();

server.Start();

Console.WriteLine("Server started");
Console.ReadKey();

static async Task DefaultRoute(HttpContext ctx) => 
    await ctx.Response.SendAsync("Hello from default route!"); 

static async Task GetUrlsRoute(HttpContext ctx) => 
    await ctx.Response.SendAsync("Here are your links!"); 

static async Task CheckLoginRoute(HttpContext ctx) => 
    await ctx.Response.SendAsync("Checking your login!"); 

static async Task TestRoute(HttpContext ctx) => 
    await ctx.Response.SendAsync("Hello from the test route!"); 
```

## Accessing from Outside Localhost

When you configure HttpServerLite to listen on ```127.0.0.1``` or ```localhost```, it will only respond to requests received from within the local machine.

To configure access from other nodes outside of ```localhost```, use the following:

- Specify the IP address on which HttpServerLite should listen in the Server constructor. 
- If you want to listen on more than one IP address, use ```*``` or ```+```
- If you listen on anything other than ```localhost``` or ```127.0.0.1```, you may have to run HttpServerLite as administrator (operating system dependent)
- If you want to use a port number less than 1024, you MUST run HttpServerLite as administrator (this is an operating system limitation)
- Open a port on your firewall to permit traffic on the TCP port upon which HttpServerLite is listening
- If you're still having problems, please do not hesitate to file an issue here, and I will do my best to help and update the documentation

## Version History

Refer to CHANGELOG.md for version history.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CavemanTcp;

namespace HttpServerLite
{
    /// <summary>
    /// HttpServerLite web server.
    /// </summary>
    public class Webserver : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Indicates if the server is listening for connections.
        /// </summary>
        public bool IsListening
        {
            get
            {
                if (_TcpServer != null) return _TcpServer.IsListening;
                return false;
            }
        }

        /// <summary>
        /// List connections by requestor IP:port.
        /// </summary>
        public IEnumerable<string> Connections
        {
            get
            {
                if (_TcpServer != null) return _TcpServer.GetClients();
                return new List<string>();
            }
        }

        /// <summary>
        /// Webserver settings.
        /// </summary>
        public WebserverSettings Settings
        {
            get
            {
                return _Settings;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Settings));
                _Settings = value;
            }
        }
           
        /// <summary>
        /// Event handlers for webserver events.
        /// </summary>
        public WebserverEvents Events
        {
            get
            {
                return _Events;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Events));
                _Events = value;
            }
        }

        /// <summary>
        /// Webserver routes.
        /// </summary>
        public WebserverRoutes Routes
        {
            get
            {
                return _Routes;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Routes));
                _Routes = value;
            }
        }

        /// <summary>
        /// Default pages served by the webserver.
        /// </summary>
        public WebserverPages Pages
        {
            get
            {
                return _Pages;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Pages));
                _Pages = value;
            }
        }

        /// <summary>
        /// Webserver statistics.
        /// </summary>
        public WebserverStatistics Statistics
        {
            get
            {
                return _Statistics;
            }
        }

        #endregion

        #region Private-Members

        private string _Header = "[HttpServerLite] ";
        private Assembly _Assembly = Assembly.GetCallingAssembly();
        private WebserverSettings _Settings = new WebserverSettings();
        private WebserverEvents _Events = new WebserverEvents();
        private WebserverRoutes _Routes = new WebserverRoutes();
        private WebserverPages _Pages = new WebserverPages();
        private WebserverStatistics _Statistics = new WebserverStatistics();
        private CavemanTcpServer _TcpServer = null;  
         
        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;
         
        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the webserver without SSL listening on localhost port 8080.
        /// </summary>
        public Webserver()
        {
            InitializeServer();
        }

        /// <summary>
        /// Instantiate the webserver without SSL listening on localhost port 8080 and using the specified default route.
        /// </summary>
        /// <param name="defaultRoute">Default route.</param>
        public Webserver(Func<HttpContext, Task> defaultRoute)
        {
            if (defaultRoute == null) throw new ArgumentNullException(nameof(defaultRoute));
             
            _Routes = new WebserverRoutes(defaultRoute);

            InitializeServer();
        }

        /// <summary>
        /// Instantiate the webserver using the specified settings.
        /// </summary>
        /// <param name="settings">Webserver settings.</param>
        public Webserver(WebserverSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _Settings = settings;

            InitializeServer();
        }

        /// <summary>
        /// Instantiate the webserver without SSL.
        /// </summary>
        /// <param name="hostname">Hostname or IP address on which to listen.</param>
        /// <param name="port">TCP port on which to listen.</param>
        /// <param name="defaultRoute">Default route.</param>
        public Webserver(string hostname, int port, Func<HttpContext, Task> defaultRoute)
        {
            if (String.IsNullOrEmpty(hostname)) hostname = "localhost";
            if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));
            if (defaultRoute == null) throw new ArgumentNullException(nameof(defaultRoute));

            _Settings = new WebserverSettings(hostname, port);
            _Routes = new WebserverRoutes(defaultRoute);

            InitializeServer(); 
        }

        /// <summary>
        /// Instantiate the webserver with or without SSL.
        /// </summary>
        /// <param name="hostname">Hostname or IP address on which to listen.</param>
        /// <param name="port">TCP port on which to listen.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">For SSL, the PFX certificate filename.</param>
        /// <param name="pfxCertPassword">For SSL, the PFX certificate password.</param>
        /// <param name="defaultRoute">Default route.</param>
        public Webserver(string hostname, int port, bool ssl, string pfxCertFilename, string pfxCertPassword, Func<HttpContext, Task> defaultRoute)
        {
            if (String.IsNullOrEmpty(hostname)) hostname = "localhost";
            if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));
            if (defaultRoute == null) throw new ArgumentNullException(nameof(defaultRoute));

            _Settings = new WebserverSettings(hostname, port);
            _Settings.Ssl.Enable = ssl;
            _Settings.Ssl.PfxCertificateFile = pfxCertFilename;
            _Settings.Ssl.PfxCertificatePassword = pfxCertPassword;

            _Routes = new WebserverRoutes(defaultRoute);

            InitializeServer(); 
        }

        /// <summary>
        /// Dispose of the object.
        /// Do not use the object after disposal.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        public void Start()
        {
            if (_TcpServer == null) throw new ObjectDisposedException("Webserver has been disposed.");
            if (_TcpServer.IsListening) throw new InvalidOperationException("Webserver is already running.");

            LoadRoutes();

            _TcpServer.Start();

            _TokenSource = new CancellationTokenSource();
            _Token = _TokenSource.Token;

            Events.HandleServerStarted(this, EventArgs.Empty);
        }
         
        /// <summary>
        /// Stop accepting new connections.
        /// </summary>
        public void Stop()
        {
            if (_TcpServer == null) throw new ObjectDisposedException("Webserver has been disposed.");
            if (!_TcpServer.IsListening) throw new InvalidOperationException("Webserver is already stopped.");

            _TcpServer.Stop();

            Events.HandleServerStopped(this, EventArgs.Empty);
        }

        #endregion

        #region Private-Methods
        
        /// <summary>
        /// Tear down the server and dispose of background workers.
        /// Do not use the object after disposal.
        /// </summary>
        /// <param name="disposing">Indicate if resources should be disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Events.HandleServerDisposing(this, EventArgs.Empty);

                if (_TcpServer != null)
                {
                    if (_TcpServer.IsListening) Stop();

                    _TcpServer.Dispose();
                    _TcpServer = null;
                }

                if (_TokenSource != null && !_Token.IsCancellationRequested)
                {
                    _TokenSource.Cancel();
                    _TokenSource.Dispose();
                }

                _Statistics = null;
                _Settings = null;
                _Routes = null;
                _Events = null;
            }
        }
         
        private void LoadRoutes()
        {
            var staticRoutes = _Assembly
                .GetTypes() // Get all classes from assembly
                .SelectMany(x => x.GetMethods()) // Get all methods from assembly
                .Where(IsStaticRoute); // Only select methods that are valid routes

            var parameterRoutes = _Assembly
                .GetTypes() // Get all classes from assembly
                .SelectMany(x => x.GetMethods()) // Get all methods from assembly
                .Where(IsParameterRoute); // Only select methods that are valid routes

            var dynamicRoutes = _Assembly
                .GetTypes() // Get all classes from assembly
                .SelectMany(x => x.GetMethods()) // Get all methods from assembly
                .Where(IsDynamicRoute); // Only select methods that are valid routes

            foreach (var staticRoute in staticRoutes)
            {
                var attribute = staticRoute.GetCustomAttributes().OfType<StaticRouteAttribute>().First();
                if (!_Routes.Static.Exists(attribute.Method, attribute.Path))
                {
                    Events.Logger?.Invoke(_Header + "adding static route " + attribute.Method.ToString() + " " + attribute.Path);
                    _Routes.Static.Add(attribute.Method, attribute.Path, ToRouteMethod(staticRoute));
                }
            }

            foreach (var parameterRoute in parameterRoutes)
            {
                var attribute = parameterRoute.GetCustomAttributes().OfType<ParameterRouteAttribute>().First();
                if (!_Routes.Parameter.Exists(attribute.Method, attribute.Path))
                {
                    Events.Logger?.Invoke(_Header + "adding parameter route " + attribute.Method.ToString() + " " + attribute.Path);
                    _Routes.Parameter.Add(attribute.Method, attribute.Path, ToRouteMethod(parameterRoute));
                }
            }

            foreach (var dynamicRoute in dynamicRoutes)
            {
                var attribute = dynamicRoute.GetCustomAttributes().OfType<DynamicRouteAttribute>().First();
                if (!_Routes.Dynamic.Exists(attribute.Method, attribute.Path))
                {
                    Events.Logger?.Invoke(_Header + "adding dynamic route " + attribute.Method.ToString() + " " + attribute.Path);
                    _Routes.Dynamic.Add(attribute.Method, attribute.Path, ToRouteMethod(dynamicRoute));
                }
            }
        }

        private bool IsStaticRoute(MethodInfo method)
        {
            return method.GetCustomAttributes().OfType<StaticRouteAttribute>().Any()
               && method.ReturnType == typeof(Task)
               && method.GetParameters().Length == 1
               && method.GetParameters().First().ParameterType == typeof(HttpContext);
        }

        private bool IsParameterRoute(MethodInfo method)
        {
            return method.GetCustomAttributes().OfType<ParameterRouteAttribute>().Any()
               && method.ReturnType == typeof(Task)
               && method.GetParameters().Length == 1
               && method.GetParameters().First().ParameterType == typeof(HttpContext);
        }

        private bool IsDynamicRoute(MethodInfo method)
        {
            return method.GetCustomAttributes().OfType<DynamicRouteAttribute>().Any()
               && method.ReturnType == typeof(Task)
               && method.GetParameters().Length == 1
               && method.GetParameters().First().ParameterType == typeof(HttpContext);
        }

        private Func<HttpContext, Task> ToRouteMethod(MethodInfo method)
        {
            if (method.IsStatic)
            {
                return (Func<HttpContext, Task>)Delegate.CreateDelegate(typeof(Func<HttpContext, Task>), method);
            }
            else
            {
                object instance = Activator.CreateInstance(method.DeclaringType ?? throw new Exception("Declaring class is null"));
                return (Func<HttpContext, Task>)Delegate.CreateDelegate(typeof(Func<HttpContext, Task>), instance, method);
            }
        }
         
        private void InitializeServer()
        { 
            if (!_Settings.Ssl.Enable)
            {
                _TcpServer = new CavemanTcpServer(
                    _Settings.Hostname,
                    _Settings.Port);

                _Header = "[HttpServerLite http://" + _Settings.Hostname + ":" + _Settings.Port + "] ";
            }
            else
            {
                _TcpServer = new CavemanTcpServer(
                    _Settings.Hostname,
                    _Settings.Port,
                    _Settings.Ssl.Enable,
                    _Settings.Ssl.PfxCertificateFile,
                    _Settings.Ssl.PfxCertificatePassword);

                _Header = "[HttpServerLite https://" + _Settings.Hostname + ":" + _Settings.Port + "] ";
            }

            _TcpServer.Settings.MonitorClientConnections = false;
            _TcpServer.Events.ClientConnected += ClientConnected;
            _TcpServer.Events.ClientDisconnected += ClientDisconnected;

            if (_Settings.Debug.Tcp)
            {
                _TcpServer.Logger = _Events.Logger;
            }
        }

        private async void ClientConnected(object sender, ClientConnectedEventArgs args)
        { 
            DateTime startTime = DateTime.Now;

            #region Parse-IP-Port

            string ipPort = args.IpPort;
            string ip = null;
            int port = 0;
            Common.ParseIpPort(ipPort, out ip, out port);
            HttpContext ctx = null;

            _Events.HandleConnectionReceived(this, new ConnectionEventArgs(ip, port));
            
            if (_Settings.Debug.Connections) 
                _Events.Logger?.Invoke(_Header + "connection from " + ip + ":" + port); 

            #endregion

            #region Process

            try
            {
                #region Retrieve-Headers

                StringBuilder sb = new StringBuilder();
                 
                //                           123456789012345 6 7 8
                // minimum request 16 bytes: GET / HTTP/1.1\r\n\r\n
                int preReadLen = 18; 
                ReadResult preReadResult = await _TcpServer.ReadWithTimeoutAsync(
                    _Settings.IO.ReadTimeoutMs, 
                    args.IpPort, 
                    preReadLen, 
                    _Token).ConfigureAwait(false);

                if (preReadResult.Status != ReadResultStatus.Success 
                    || preReadResult.BytesRead != preReadLen 
                    || preReadResult.Data == null 
                    || preReadResult.Data.Length != preReadLen) return;

                sb.Append(Encoding.ASCII.GetString(preReadResult.Data));

                bool retrievingHeaders = true;
                while (retrievingHeaders)
                {
                    if (sb.ToString().EndsWith("\r\n\r\n"))
                    {
                        retrievingHeaders = false;
                    }
                    else
                    {
                        if (sb.Length >= _Settings.IO.MaxIncomingHeadersSize)
                        {
                            _Events.Logger?.Invoke(_Header + "failed to read headers from " + ip + ":" + port + " within " + _Settings.IO.MaxIncomingHeadersSize + " bytes, closing connection");
                            return;
                        }

                        ReadResult addlReadResult = await _TcpServer.ReadWithTimeoutAsync(
                            _Settings.IO.ReadTimeoutMs, 
                            args.IpPort, 
                            1, 
                            _Token).ConfigureAwait(false);

                        if (addlReadResult.Status == ReadResultStatus.Success)
                        {
                            sb.Append(Encoding.ASCII.GetString(addlReadResult.Data));
                        }
                        else
                        {
                            return;
                        }
                    }
                } 

                #endregion

                #region Build-Context

                ctx = new HttpContext(
                    ipPort, 
                    _TcpServer.GetStream(ipPort), 
                    sb.ToString(), 
                    Events, 
                    _Settings.Headers,
                    _Settings.IO.StreamBufferSize);

                _Statistics.IncrementRequestCounter(ctx.Request.Method);
                _Statistics.AddReceivedPayloadBytes(ctx.Request.ContentLength);

                _Events.HandleRequestReceived(this, new RequestEventArgs(ctx));

                if (_Settings.Debug.Requests)
                {
                    _Events.Logger?.Invoke(
                        _Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                }

                #endregion
                 
                #region Check-Access-Control

                if (!_Settings.AccessControl.Permit(ctx.Request.Source.IpAddress))
                {
                    _Events.HandleRequestDenied(this, new RequestEventArgs(ctx));

                    if (_Settings.Debug.AccessControl)
                    {
                        _Events.Logger?.Invoke(_Header + "request from " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " denied due to access control");
                    }

                    return;
                }

                #endregion

                #region Process-Preflight-Requests

                if (ctx.Request.Method == HttpMethod.OPTIONS)
                {
                    if (_Routes.Preflight != null)
                    {
                        if (_Settings.Debug.Routing)
                        {
                            _Events.Logger?.Invoke(
                                _Header + "preflight route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " + 
                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                        }

                        await _Routes.Preflight(ctx).ConfigureAwait(false);
                        return;
                    }
                }

                #endregion

                #region Pre-Routing-Handler

                bool terminate = false;
                if (_Routes.PreRouting != null)
                {
                    terminate = await _Routes.PreRouting(ctx).ConfigureAwait(false);
                    if (terminate)
                    {
                        if (_Settings.Debug.Routing)
                        {
                            _Events.Logger?.Invoke(
                                _Header + "prerouting terminated connection for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                        }

                        return;
                    }
                }

                #endregion

                #region Content-Routes

                if (ctx.Request.Method == HttpMethod.GET || ctx.Request.Method == HttpMethod.HEAD)
                {
                    if (_Routes.Content.Exists(ctx.Request.Url.WithoutQuery))
                    {
                        if (_Settings.Debug.Routing)
                        {
                            _Events.Logger?.Invoke(
                                _Header + "content route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                        }

                        await _Routes.ContentHandler.Process(ctx, _Token).ConfigureAwait(false);
                        return;
                    }
                }

                #endregion

                #region Static-Routes

                Func<HttpContext, Task> handler = _Routes.Static.Match(ctx.Request.Method, ctx.Request.Url.WithoutQuery);
                if (handler != null)
                {
                    if (_Settings.Debug.Routing)
                    {
                        _Events.Logger?.Invoke(
                            _Header + "static route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.WithoutQuery);
                    }

                    await handler(ctx).ConfigureAwait(false);
                    return;
                }

                #endregion

                #region Parameter-Routes

                Dictionary<string, string> parameters = null;
                handler = _Routes.Parameter.Match(ctx.Request.Method, ctx.Request.Url.WithoutQuery, out parameters);
                if (handler != null)
                {
                    ctx.Request.Url.Parameters = new Dictionary<string, string>(parameters);

                    if (_Settings.Debug.Routing)
                    {
                        _Events.Logger?.Invoke(
                            _Header + "parameter route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.WithoutQuery);
                    }

                    await handler(ctx).ConfigureAwait(false);
                    return;
                }

                #endregion

                #region Dynamic-Routes

                handler = _Routes.Dynamic.Match(ctx.Request.Method, ctx.Request.Url.WithoutQuery);
                if (handler != null)
                {
                    if (_Settings.Debug.Routing)
                    {
                        _Events.Logger?.Invoke(
                            _Header + "dynamic route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                    }

                    await handler(ctx).ConfigureAwait(false);
                    return;
                }

                #endregion

                #region Default-Route

                if (_Settings.Debug.Routing)
                {
                    _Events.Logger?.Invoke(
                        _Header + "default route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                }

                if (_Routes.Default != null)
                {
                    await _Routes.Default(ctx).ConfigureAwait(false);
                    return;
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    ctx.Response.ContentType = _Pages.Default404Page.ContentType;
                    await ctx.Response.SendAsync(_Pages.Default404Page.Content, _Token).ConfigureAwait(false);
                }

                #endregion  
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception e)
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = _Pages.Default500Page.ContentType;
                await ctx.Response.SendAsync(_Pages.Default500Page.Content, _Token).ConfigureAwait(false);

                _Events.Logger?.Invoke(_Header + "exception: " + Environment.NewLine + SerializationHelper.SerializeJson(e, true));
                _Events.HandleException(this, new ExceptionEventArgs(ctx, e));
                return;
            }
            finally
            { 
                _TcpServer.DisconnectClient(ipPort);

                if (ctx != null)
                {
                    double totalMs = TotalMsFrom(startTime);

                    _Events.HandleResponseSent(this, new ResponseEventArgs(ctx, totalMs));

                    if (_Settings.Debug.Responses)
                    { 
                        _Events.Logger?.Invoke(
                            _Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full + ": " +
                            ctx.Response.StatusCode + " [" + totalMs + "ms]");
                    }

                    if (ctx.Response.ContentLength != null)
                    {
                        _Statistics.AddSentPayloadBytes(Convert.ToInt64(ctx.Response.ContentLength));
                    }
                }
            }

            #endregion
        }

        private void ClientDisconnected(object sender, ClientDisconnectedEventArgs args)
        {

        }
        
        private double TotalMsFrom(DateTime startTime)
        {
            try
            {
                DateTime endTime = DateTime.Now;
                TimeSpan totalTime = (endTime - startTime);
                return totalTime.TotalMilliseconds;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        #endregion
    }
}

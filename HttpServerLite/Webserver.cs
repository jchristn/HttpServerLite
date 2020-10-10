using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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
        /// For SSL, accept or deny invalid or otherwise unverifiable SSL certificates.
        /// </summary>
        public bool AcceptInvalidCertificates
        {
            get
            {
                return _TcpServer.Settings.AcceptInvalidCertificates;
            }
            set
            {
                _TcpServer.Settings.AcceptInvalidCertificates = value;
            }
        }

        /// <summary>
        /// For SSL, enable to require mutual authentication.
        /// </summary>
        public bool MutuallyAuthenticate
        {
            get
            {
                return _TcpServer.Settings.MutuallyAuthenticate;
            }
            set
            {
                _TcpServer.Settings.MutuallyAuthenticate = value;
            }
        }
         
        /// <summary>
        /// Buffer size to use when interacting with streams.
        /// </summary>
        public int StreamReadBufferSize
        {
            get
            {
                return _StreamReadBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentException("StreamReadBufferSize must be greater than zero.");
                _StreamReadBufferSize = value;
            }
        }

        /// <summary>
        /// Set specific actions/callbacks to use when events are raised.
        /// </summary>
        public EventCallbacks Events = new EventCallbacks();

        /// <summary>
        /// Webserver statistics.
        /// </summary>
        public Statistics Stats
        {
            get
            {
                return _Stats;
            }
        }

        /// <summary>
        /// Headers that will be added to every response unless previously set.
        /// </summary>
        public DefaultHeaderValues DefaultHeaders
        {
            get
            {
                return _DefaultHeaders;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(DefaultHeaders));
                _DefaultHeaders = value;
            }
        }

        /// <summary>
        /// Access control manager, i.e. default mode of operation, permit list, and deny list.
        /// </summary>
        public AccessControlManager AccessControl = new AccessControlManager(AccessControlMode.DefaultPermit);

        /// <summary>
        /// Function to call prior to routing.  
        /// Return 'true' if the connection should be terminated.
        /// Return 'false' to allow the connection to continue routing.
        /// </summary>
        public Func<HttpContext, Task<bool>> PreRoutingHandler = null;

        /// <summary>
        /// Function to call when an OPTIONS request is received.  Often used to handle CORS.  Leave as 'null' to use the default OPTIONS handler.
        /// </summary>
        public Func<HttpContext, Task> OptionsRoute = null;

        /// <summary>
        /// Content routes; i.e. routes to specific files or folders for GET and HEAD requests.
        /// </summary>
        public ContentRouteManager ContentRoutes = new ContentRouteManager();

        /// <summary>
        /// Static routes; i.e. routes with explicit matching and any HTTP method.
        /// </summary>
        public StaticRouteManager StaticRoutes = new StaticRouteManager();

        /// <summary>
        /// Dynamic routes; i.e. routes with regex matching and any HTTP method.
        /// </summary>
        public DynamicRouteManager DynamicRoutes = new DynamicRouteManager();

        #endregion

        #region Private-Members

        private string _Hostname = null;
        private int _Port = 0;
        private bool _Ssl = false;
        private string _PfxCertFilename = null;
        private string _PfxCertPassword = null;
        private CavemanTcpServer _TcpServer = null; 
        private DefaultHeaderValues _DefaultHeaders = new DefaultHeaderValues();
        private Func<HttpContext, Task> _DefaultRoute = null;
        private ContentRouteProcessor _ContentRouteProcessor;

        private int _StreamReadBufferSize = 65536;
        private Statistics _Stats = new Statistics();
         
        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the webserver.
        /// </summary>
        /// <param name="hostname">Hostname or IP address on which to listen.</param>
        /// <param name="port">TCP port on which to listen.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">For SSL, the PFX certificate filename.</param>
        /// <param name="pfxCertPassword">For SSL, the PFX certificate password.</param>
        /// <param name="defaultRoute">Default route.</param>
        public Webserver(string hostname, int port, bool ssl, string pfxCertFilename, string pfxCertPassword, Func<HttpContext, Task> defaultRoute)
        {
            _Hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
            _DefaultRoute = defaultRoute ?? throw new ArgumentNullException(nameof(defaultRoute));

            _Port = port;
            _Ssl = ssl;
            _PfxCertFilename = pfxCertFilename;
            _PfxCertPassword = pfxCertPassword;
             
            _ContentRouteProcessor = new ContentRouteProcessor(ContentRoutes);

            _TcpServer = new CavemanTcpServer(_Hostname, _Port, _Ssl, _PfxCertFilename, _PfxCertPassword);
            _TcpServer.Settings.MonitorClientConnections = false;
            _TcpServer.Events.ClientConnected += ClientConnected;
            _TcpServer.Events.ClientDisconnected += ClientDisconnected;

            _Token = _TokenSource.Token;
        }

        /// <summary>
        /// Dispose of the object.
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
            _TcpServer.Start();
            Task.Run(() => Events.ServerStarted(), _Token);
        }
         
        /// <summary>
        /// Stop accepting new connections.
        /// </summary>
        public void Stop()
        {
            _TcpServer.Stop();
            Task.Run(() => Events.ServerStopped(), _Token);
        }

        #endregion

        #region Private-Methods
        
        /// <summary>
        /// Tear down the server and dispose of background workers.
        /// </summary>
        /// <param name="disposing">Indicate if resources should be disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_TcpServer != null)
                {
                    _TcpServer.Dispose();
                }

                if (_TokenSource != null && _Token != null)
                {
                    _TokenSource.Cancel();
                }

                _Stats = null;
                _DefaultHeaders = null;
                _DefaultRoute = null;

                AccessControl = null;
                OptionsRoute = null;
                ContentRoutes = null;
                StaticRoutes = null;
                DynamicRoutes = null;
                 
                Task dispTask = Task.Run(() => Events.ServerDisposed());
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
            Task connRecvTask = Task.Run(() => Events.ConnectionReceived(ip, port), _Token);

            #endregion

            #region Process

            try
            {
                #region Retrieve-Headers

                bool retrievingHeaders = true;
                byte[] headerTest = new byte[4];
                for (int i = 0; i < 4; i++) headerTest[i] = 0x00;
                byte[] headerBytes = new byte[0];

                while (retrievingHeaders)
                {
                    ReadResult rr = _TcpServer.Read(args.IpPort, 1);
                    if (rr.Status == ReadResultStatus.Success)
                    {
                        headerTest = Common.ByteArrayShiftLeft(headerTest);
                        headerTest[3] = rr.Data[0];

                        if (((int)headerTest[3]) == 10
                            && ((int)headerTest[2]) == 13
                            && ((int)headerTest[1]) == 10
                            && ((int)headerTest[0]) == 13)
                        {
                            // end of headers detected
                            retrievingHeaders = false;
                        }
                        else
                        {
                            headerBytes = Common.AppendBytes(headerBytes, rr.Data);
                        }
                    }
                    else
                    {
                        return;
                    }
                } 

                #endregion

                #region Build-Context

                HttpContext ctx = new HttpContext(
                    ipPort, 
                    _TcpServer.GetStream(ipPort), 
                    headerBytes, 
                    Events, 
                    _DefaultHeaders);

                _Stats.IncrementRequestCounter(ctx.Request.Method);
                _Stats.ReceivedPayloadBytes += ctx.Request.ContentLength;

                Task reqRecvTask = Task.Run(() => Events.RequestReceived(
                    ctx.Request.SourceIp,
                    ctx.Request.SourcePort,
                    ctx.Request.Method.ToString(),
                    ctx.Request.RawUrlWithQuery),
                    _Token);

                #endregion

                #region Process

                try
                {
                    #region Check-Access-Control

                    if (!AccessControl.Permit(ctx.Request.SourceIp))
                    {
                        Task aclDenied = Task.Run(() => Events.AccessControlDenied(
                            ctx.Request.SourceIp,
                            ctx.Request.SourcePort,
                            ctx.Request.Method.ToString(),
                            ctx.Request.FullUrl),
                            _Token);
                        return;
                    }

                    #endregion

                    #region Process-Preflight-Requests

                    if (ctx.Request.Method == HttpMethod.OPTIONS)
                    {
                        if (OptionsRoute != null)
                        {
                            await OptionsRoute?.Invoke(ctx);
                            return;
                        }
                        else
                        {
                            await OptionsProcessor(ctx);
                            return;
                        }
                    }

                    #endregion

                    #region Pre-Routing-Handler

                    bool terminate = false;
                    if (PreRoutingHandler != null)
                    {
                        terminate = await PreRoutingHandler(ctx);
                        if (terminate) return;
                    }

                    #endregion

                    #region Content-Routes

                    if (ctx.Request.Method == HttpMethod.GET || ctx.Request.Method == HttpMethod.HEAD)
                    {
                        if (ContentRoutes.Exists(ctx.Request.RawUrlWithoutQuery))
                        {
                            await _ContentRouteProcessor.Process(ctx);
                            return;
                        }
                    }

                    #endregion

                    #region Static-Routes

                    Func<HttpContext, Task> handler = StaticRoutes.Match(ctx.Request.Method, ctx.Request.RawUrlWithoutQuery);
                    if (handler != null)
                    {
                        await handler(ctx);
                        return;
                    }

                    #endregion

                    #region Dynamic-Routes

                    handler = DynamicRoutes.Match(ctx.Request.Method, ctx.Request.RawUrlWithoutQuery);
                    if (handler != null)
                    {
                        await handler(ctx);
                        return;
                    }

                    #endregion

                    #region Default-Route

                    await _DefaultRoute?.Invoke(ctx);

                    #endregion
                }
                finally
                {
                    Task respSentTask = Task.Run(() => Events.ResponseSent(
                        ctx.Request.SourceIp,
                        ctx.Request.SourcePort,
                        ctx.Request.Method.ToString(),
                        ctx.Request.FullUrl,
                        ctx.Response.StatusCode,
                        Common.TotalMsFrom(startTime)),
                        _Token);

                    if (ctx.Response.ContentLength != null)
                        _Stats.SentPayloadBytes += Convert.ToInt64(ctx.Response.ContentLength);

                    _TcpServer.DisconnectClient(ipPort);
                }

                #endregion
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception e)
            {
                Task excTask = Task.Run(() => Events.ExceptionEncountered(ip, port, e), _Token);
                return;
            }

            #endregion
        }

        private void ClientDisconnected(object sender, ClientDisconnectedEventArgs args)
        {

        }

        private async Task OptionsProcessor(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;

            string[] requestedHeaders = null;
            if (ctx.Request.Headers != null)
            {
                foreach (KeyValuePair<string, string> curr in ctx.Request.Headers)
                {
                    if (String.IsNullOrEmpty(curr.Key)) continue;
                    if (String.IsNullOrEmpty(curr.Value)) continue;
                    if (String.Compare(curr.Key.ToLower(), "access-control-request-headers") == 0)
                    {
                        requestedHeaders = curr.Value.Split(',');
                        break;
                    }
                }
            }

            string headers = "";

            if (requestedHeaders != null)
            {
                int addedCount = 0;
                foreach (string curr in requestedHeaders)
                {
                    if (String.IsNullOrEmpty(curr)) continue;
                    if (addedCount > 0) headers += ", ";
                    headers += ", " + curr;
                    addedCount++;
                }
            }
             
            ctx.Response.ContentLength = 0;
            await ctx.Response.SendAsync(0);
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CavemanTcp;

namespace HttpServerLite
{
    /// <summary>
    /// HttpServerLite web server.
    /// </summary>
    public class Webserver
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
        /// Method to invoke when sending log messages.
        /// </summary>
        public Action<string> Logger = null;

        /// <summary>
        /// For SSL, accept or deny invalid or otherwise unverifiable SSL certificates.
        /// </summary>
        public bool AcceptInvalidCertificates
        {
            get
            {
                return _TcpServer.AcceptInvalidCertificates;
            }
            set
            {
                _TcpServer.AcceptInvalidCertificates = value;
            }
        }

        /// <summary>
        /// For SSL, enable to require mutual authentication.
        /// </summary>
        public bool MutuallyAuthenticate
        {
            get
            {
                return _TcpServer.MutuallyAuthenticate;
            }
            set
            {
                _TcpServer.MutuallyAuthenticate = value;
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
        /// Access-Control-Allow-Origin header value.
        /// </summary>
        public string AccessControlAllowOriginHeader = "*";

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
        private TcpServer _TcpServer = null;
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

            _TcpServer = new TcpServer(_Hostname, _Port, _Ssl, _PfxCertFilename, _PfxCertPassword);
            _TcpServer.ClientConnected += ClientConnected;
            _TcpServer.ClientDisconnected += ClientDisconnected;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the server.
        /// </summary>
        public void Start()
        {
            _TcpServer.Start();
        }

        #endregion

        #region Private-Methods

        private async void ClientConnected(object sender, ClientConnectedEventArgs args)
        { 
            DateTime startTime = DateTime.Now;

            #region Parse-IP-Port

            string ipPort = args.IpPort;
            string ip = null;
            int port = 0;
            Common.ParseIpPort(ipPort, out ip, out port);
            Events.ConnectionReceived?.Invoke(ip, port);

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

                HttpContext ctx = new HttpContext(ipPort, _TcpServer.GetStream(ipPort), headerBytes, Events);

                _Stats.IncrementRequestCounter(ctx.Request.Method);
                _Stats.ReceivedPayloadBytes += ctx.Request.ContentLength;

                Events.RequestReceived?.Invoke(
                    ctx.Request.SourceIp,
                    ctx.Request.SourcePort,
                    ctx.Request.Method.ToString(),
                    ctx.Request.RawUrlWithQuery);

                #endregion

                #region Process

                try
                {
                    #region Check-Access-Control

                    if (!AccessControl.Permit(ctx.Request.SourceIp))
                    {
                        Events.AccessControlDenied?.Invoke(
                            ctx.Request.SourceIp,
                            ctx.Request.SourcePort,
                            ctx.Request.Method.ToString(),
                            ctx.Request.FullUrl);
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
                    Events.ResponseSent?.Invoke(
                        ctx.Request.SourceIp,
                        ctx.Request.SourcePort,
                        ctx.Request.Method.ToString(),
                        ctx.Request.FullUrl,
                        ctx.Response.StatusCode,
                        Common.TotalMsFrom(startTime));

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
                Events.ExceptionEncountered(ip, port, e);
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

            string listenerPrefix = null;
            if (_Ssl) listenerPrefix = "https://" + ctx.Request.DestHostname + ":" + _Port + "/";
            else listenerPrefix = "http://" + ctx.Request.DestHostname + ":" + _Port + "/";

            ctx.Response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS, HEAD, GET, PUT, POST, DELETE");
            ctx.Response.Headers.Add("Access-Control-Allow-Headers", "*, Content-Type, X-Requested-With, " + headers);
            ctx.Response.Headers.Add("Access-Control-Expose-Headers", "Content-Type, X-Requested-With, " + headers);

            if (!String.IsNullOrEmpty(AccessControlAllowOriginHeader))
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", AccessControlAllowOriginHeader);

            ctx.Response.Headers.Add("Accept", "*/*");
            ctx.Response.Headers.Add("Accept-Language", "en-US, en");
            ctx.Response.Headers.Add("Accept-Charset", "ISO-8859-1, utf-8");
            ctx.Response.Headers.Add("Connection", "keep-alive");
            ctx.Response.Headers.Add("Host", listenerPrefix);

            ctx.Response.ContentLength = 0;
            await ctx.Response.SendAsync(0);
        }

        #endregion
    }
}

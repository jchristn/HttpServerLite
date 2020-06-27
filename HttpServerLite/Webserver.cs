using System;
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

        #endregion

        #region Private-Members

        private string _Hostname = null;
        private int _Port = 0;
        private bool _Ssl = false;
        private string _PfxCertFilename = null;
        private string _PfxCertPassword = null;
        private TcpServer _TcpServer = null;
        private Func<HttpContext, Task> _DefaultRoute = null;
        private int _StreamReadBufferSize = 65536;
        private Statistics _Stats = new Statistics();

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

            #region Retrieve-Headers

            bool retrievingHeaders = true;
            byte[] headerTest = new byte[4];
            for (int i = 0; i < 4; i++) headerTest[i] = 0x00;
            byte[] headerBytes = new byte[0];

            while (retrievingHeaders)
            {
                byte[] b = _TcpServer.ReadBytes(args.IpPort, 1);

                headerTest = Common.ByteArrayShiftLeft(headerTest);
                headerTest[3] = b[0];

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
                    headerBytes = Common.AppendBytes(headerBytes, b);
                }
            }

            #endregion

            #region Build-Context-and-Send-Event

            HttpContext ctx = new HttpContext(ipPort, _TcpServer.GetStream(ipPort), headerBytes, Events);

            _Stats.IncrementRequestCounter(ctx.Request.Method);
            _Stats.ReceivedPayloadBytes += ctx.Request.ContentLength;

            Events.RequestReceived?.Invoke(
                ctx.Request.SourceIp, 
                ctx.Request.SourcePort, 
                ctx.Request.Method.ToString(), 
                ctx.Request.RawUrlWithQuery);

            try
            {
                await _DefaultRoute?.Invoke(ctx);
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
            }

            _TcpServer.DisconnectClient(ipPort);

            #endregion
        }

        private void ClientDisconnected(object sender, ClientDisconnectedEventArgs args)
        {

        }

        #endregion
    }
}

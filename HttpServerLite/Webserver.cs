using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CavemanTcp;

namespace HttpServerLite
{
    public class Webserver
    {
        public Action<string> Logger = null;
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

        private string _Hostname = null;
        private int _Port = 0;
        private bool _Ssl = false;
        private string _PfxCertFilename = null;
        private string _PfxCertPassword = null;
        private TcpServer _TcpServer = null;
        private Action<HttpContext> _DefaultRoute = null;
        private int _StreamReadBufferSize = 65536;

        public Webserver(string hostname, int port, bool ssl, string pfxCertFilename, string pfxCertPassword, Action<HttpContext> defaultRoute)
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

        public void Start()
        {
            _TcpServer.Start();
        }

        private void ClientConnected(object sender, ClientConnectedEventArgs args)
        {
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
            _DefaultRoute?.Invoke(ctx);
            _TcpServer.DisconnectClient(ipPort);

            #endregion
        }

        private void ClientDisconnected(object sender, ClientDisconnectedEventArgs args)
        {

        } 
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace HttpServerLite
{
    /// <summary>
    /// Webserver settings.
    /// </summary>
    public class WebserverSettings
    {
        #region Public-Members

        /// <summary>
        /// The hostname or IP address on which to listen.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
        }

        /// <summary>
        /// The port number on which to listen.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
        }

        /// <summary>
        /// Input-output settings.
        /// </summary>
        public IOSettings IO
        {
            get
            {
                return _IO;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(IO));
                _IO = value;
            }
        }

        /// <summary>
        /// SSL settings.
        /// </summary>
        public SslSettings Ssl
        {
            get
            {
                return _Ssl;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Ssl));
                _Ssl = value;
            }
        }

        /// <summary>
        /// Headers that will be added to every response unless previously set.
        /// </summary>
        public HeaderSettings Headers
        {
            get
            {
                return _Headers;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Headers));
                _Headers = value;
            }
        }

        /// <summary>
        /// Access control manager, i.e. default mode of operation, permit list, and deny list.
        /// </summary>
        public AccessControlManager AccessControl
        {
            get
            {
                return _AccessControl;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(AccessControl));
                _AccessControl = value;
            }
        }

        /// <summary>
        /// Debug logging settings.
        /// Be sure to set Events.Logger in order to receive debug messages.
        /// </summary>
        public DebugSettings Debug
        {
            get
            {
                return _Debug;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Debug));
                _Debug = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 8080;

        private IOSettings _IO = new IOSettings();
        private SslSettings _Ssl = new SslSettings();
        private HeaderSettings _Headers = new HeaderSettings();
        private AccessControlManager _AccessControl = new AccessControlManager(AccessControlMode.DefaultPermit);
        private DebugSettings _Debug = new DebugSettings();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object using default settings.
        /// </summary>
        public WebserverSettings()
        {

        }

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="hostname">The hostname on which to listen.</param>
        /// <param name="port">The port on which to listen.</param>
        public WebserverSettings(string hostname, int port)
        {
            if (String.IsNullOrEmpty(hostname)) hostname = "localhost";
            if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));

            _Hostname = hostname;
            _Port = port;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion

        #region Public-Classes

        /// <summary>
        /// Input-output settings.
        /// </summary>
        public class IOSettings
        {
            /// <summary>
            /// Maximum number of bytes to read from the network in attempt to read incoming HTTP request headers.
            /// </summary>
            public int MaxIncomingHeadersSize
            {
                get
                {
                    return _MaxIncomingHeadersSize;
                }
                set
                {
                    if (value < 1) throw new ArgumentOutOfRangeException(nameof(MaxIncomingHeadersSize));
                    _MaxIncomingHeadersSize = value;
                }
            }

            /// <summary>
            /// Buffer size to use when interacting with streams.
            /// </summary>
            public int StreamBufferSize
            {
                get
                {
                    return _StreamBufferSize;
                }
                set
                {
                    if (value < 1) throw new ArgumentOutOfRangeException(nameof(StreamBufferSize));
                    _StreamBufferSize = value;
                }
            }

            /// <summary>
            /// Number of milliseconds to await a read response prior to considering the connection to have timed out.
            /// </summary>
            public int ReadTimeoutMs
            {
                get
                {
                    return _ReadTimeoutMs;
                }
                set
                {
                    if (value < 1) throw new ArgumentOutOfRangeException(nameof(ReadTimeoutMs));
                    _ReadTimeoutMs = value;
                }
            }

            private int _MaxIncomingHeadersSize = 65536;
            private int _StreamBufferSize = 65536;
            private int _ReadTimeoutMs = 5000;

            /// <summary>
            /// Instantiate the object using default values.
            /// </summary>
            public IOSettings()
            {

            }
        }

        /// <summary>
        /// SSL settings.
        /// </summary>
        public class SslSettings
        {
            /// <summary>
            /// Enable or disable SSL.
            /// </summary>
            public bool Enable = false;

            /// <summary>
            /// PFX certificate filename.
            /// </summary>
            public string PfxCertificateFile = null;

            /// <summary>
            /// PFX certificate password.
            /// </summary>
            public string PfxCertificatePassword = null;

            /// <summary>
            /// Require mutual authentication.
            /// </summary>
            public bool MutuallyAuthenticate = false;

            /// <summary>
            /// Accept invalid certificates including self-signed and those that are unable to be verified.
            /// </summary>
            public bool AcceptInvalidAcertificates = true;

            /// <summary>
            /// Instantiate the object using default settings.
            /// </summary>
            public SslSettings()
            {

            }

            /// <summary>
            /// Enable SSL using a certificate file that doesn't require a password.
            /// </summary>
            /// <param name="pfxCertificateFile">PFX certificate filename.</param>
            public SslSettings(string pfxCertificateFile)
            {
                if (String.IsNullOrEmpty(pfxCertificateFile)) throw new ArgumentNullException(nameof(pfxCertificateFile));

                Enable = true;
                PfxCertificateFile = pfxCertificateFile;
            }

            /// <summary>
            /// Enable SSL using a certificate file that requires a password.
            /// </summary>
            /// <param name="pfxCertificateFile">PFX certificate filename.</param>
            /// <param name="pfxCertificatePassword">PFX certificate password.</param>
            public SslSettings(string pfxCertificateFile, string pfxCertificatePassword)
            {
                if (String.IsNullOrEmpty(pfxCertificateFile)) throw new ArgumentNullException(nameof(pfxCertificateFile));
                if (String.IsNullOrEmpty(pfxCertificatePassword)) throw new ArgumentNullException(nameof(pfxCertificatePassword));

                Enable = true;
                PfxCertificateFile = pfxCertificateFile;
                PfxCertificatePassword = pfxCertificatePassword;
            }
        }
         
        /// <summary>
        /// Headers that will be added to every response unless previously set.
        /// </summary>
        public class HeaderSettings
        {
            /// <summary>
            /// Automatically set content length if not already set.
            /// </summary>
            public bool IncludeContentLength = true;

            /// <summary>
            /// Access-Control-Allow-Origin header.
            /// </summary>
            public string AccessControlAllowOrigin = "*";

            /// <summary>
            /// Access-Control-Allow-Methods header.
            /// </summary>
            public string AccessControlAllowMethods = "OPTIONS, HEAD, GET, PUT, POST, DELETE";

            /// <summary>
            /// Access-Control-Allow-Headers header.
            /// </summary>
            public string AccessControlAllowHeaders = "*";

            /// <summary>
            /// Access-Control-Expose-Headers header.
            /// </summary>
            public string AccessControlExposeHeaders = "";

            /// <summary>
            /// Accept header.
            /// </summary>
            public string Accept = "*/*";

            /// <summary>
            /// Accept-Language header.
            /// </summary>
            public string AcceptLanguage = "en-US, en";

            /// <summary>
            /// Accept-Charset header.
            /// </summary>
            public string AcceptCharset = "ISO-8859-1, utf-8";

            /// <summary>
            /// Connection header.
            /// </summary>
            public string Connection = "close";

            /// <summary>
            /// Host header.
            /// </summary>
            public string Host = null;

            /// <summary>
            /// Instantiate the object.
            /// </summary> 
            public HeaderSettings()
            {

            }
        }

        /// <summary>
        /// Debug logging settings.
        /// Be sure to set Events.Logger in order to receive debug messages.
        /// </summary>
        public class DebugSettings
        {
            /// <summary>
            /// Enable or disable debug logging of access control.
            /// </summary>
            public bool AccessControl = false;

            /// <summary>
            /// Enable or disable debug logging of routing.
            /// </summary>
            public bool Routing = false;

            /// <summary>
            /// Enable or disable debug logging of connections.
            /// </summary>
            public bool Connections = false;

            /// <summary>
            /// Enable or disable debug logging of the underlying TCP library.
            /// </summary>
            public bool Tcp = false;

            /// <summary>
            /// Enable or disable debug logging of requests.
            /// </summary>
            public bool Requests = false;

            /// <summary>
            /// Enable or disable debug logging of responses.
            /// </summary>
            public bool Responses = false;

            /// <summary>
            /// Instantiate the object.
            /// </summary> 
            public DebugSettings()
            {

            }
        }

        #endregion
    }
}

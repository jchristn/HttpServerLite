using System; 
using System.Net;
using CavemanTcp;

namespace HttpServerLite
{
    /// <summary>
    /// HTTP context including both request and response.
    /// </summary>
    public class HttpContext
    {
        #region Public-Members
         
        /// <summary>
        /// The HTTP request that was received.
        /// </summary>
        public HttpRequest Request;

        /// <summary>
        /// The HTTP response that will be sent.  This object is preconstructed on your behalf and can be modified directly.
        /// </summary>
        public HttpResponse Response;

        /// <summary>
        /// Buffer size to use while writing the response from a supplied stream. 
        /// </summary>
        public int StreamBufferSize
        {
            get
            {
                return _StreamBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentException("StreamBufferSize must be greater than zero bytes.");
                _StreamBufferSize = value;
            }
        }

        #endregion
         
        private int _StreamBufferSize = 65536; 
        private EventCallbacks _Events;

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpContext()
        {

        }

        internal HttpContext(string ipPort, byte[] headerBytes, TcpServer server, EventCallbacks events)
        { 
            _Events = events ?? throw new ArgumentNullException(nameof(events));
            if (headerBytes == null) throw new ArgumentNullException(nameof(headerBytes));
            if (server == null) throw new ArgumentNullException(nameof(server));

            Request = new HttpRequest(ipPort, headerBytes, server);
            Response = new HttpResponse(ipPort, Request, server, _Events, _StreamBufferSize);
        }
    }
}
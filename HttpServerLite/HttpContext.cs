using System;
using System.IO;
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

        #region Private-Members

        private int _StreamBufferSize = 65536; 
        private EventCallbacks _Events;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpContext()
        {

        }

        internal HttpContext(string ipPort, Stream stream, byte[] headerBytes, EventCallbacks events)
        { 
            _Events = events ?? throw new ArgumentNullException(nameof(events));
            if (headerBytes == null) throw new ArgumentNullException(nameof(headerBytes));
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            Request = new HttpRequest(ipPort, stream, headerBytes);
            Response = new HttpResponse(ipPort, stream, Request, _Events, _StreamBufferSize);
        }

        #endregion
    }
}
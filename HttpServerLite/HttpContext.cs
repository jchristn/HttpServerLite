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
        public HttpRequest Request { get; private set; } = null;

        /// <summary>
        /// The HTTP response that will be sent.  This object is preconstructed on your behalf and can be modified directly.
        /// </summary>
        public HttpResponse Response { get; private set; } = null;
         
        #endregion

        #region Private-Members
         
        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpContext()
        {

        }

        internal HttpContext(
            string ipPort, 
            Stream stream, 
            string requestHeader, 
            WebserverEvents events, 
            WebserverSettings.HeaderSettings headers,
            int streamBufferSize)
        { 
            if (String.IsNullOrEmpty(requestHeader)) throw new ArgumentNullException(nameof(requestHeader));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            if (streamBufferSize < 1) throw new ArgumentOutOfRangeException(nameof(streamBufferSize));

            Request = new HttpRequest(ipPort, stream, requestHeader);
            Response = new HttpResponse(ipPort, headers, stream, Request, events, streamBufferSize);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve a JSON-encoded version of the context object.
        /// </summary>
        /// <param name="pretty">True to enable pretty print.</param>
        /// <returns>JSON string.</returns>
        public string ToJson(bool pretty)
        {
            return SerializationHelper.SerializeJson(this, pretty);
        }

        #endregion
    }
}
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CavemanTcp;

namespace HttpServerLite
{
    /// <summary>
    /// Response to an HTTP request.
    /// </summary>
    public class HttpResponse
    {
        #region Public-Members

        /// <summary>
        /// The protocol and version.
        /// </summary>
        [JsonPropertyOrder(-3)]
        public string ProtocolVersion { get; private set; } = null;

        /// <summary>
        /// The HTTP status code to return to the requestor (client).
        /// </summary>
        [JsonPropertyOrder(-2)]
        public int StatusCode
        {
            get
            {
                return _StatusCode;
            }
            set
            {
                if (value < 100 || value > 599) throw new ArgumentOutOfRangeException(nameof(StatusCode));
                _StatusCode = value;
            }
        }

        /// <summary>
        /// The HTTP status description to return to the requestor (client).
        /// </summary>
        [JsonPropertyOrder(-1)]
        public string StatusDescription = "OK";

        /// <summary>
        /// User-supplied headers to include in the response.
        /// </summary>
        public NameValueCollection Headers
        {
            get
            {
                return _Headers;
            }
            set
            {
                if (value == null) _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                else _Headers = value;
            }
        }

        /// <summary>
        /// User-supplied content-type to include in the response.
        /// </summary>
        [JsonPropertyOrder(990)]
        public string ContentType = null;

        /// <summary>
        /// The length of the supplied response data.
        /// </summary>
        [JsonPropertyOrder(991)]
        public long? ContentLength = null;

        #endregion

        #region Internal-Members

        internal bool ResponseSent = false; 
        internal bool HeadersSent = false;

        #endregion

        #region Private-Members

        private string _IpPort;
        private WebserverSettings.HeaderSettings _HeaderSettings = null;
        private int _StreamBufferSize = 65536;
        private NameValueCollection _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
        private Stream _Stream;
        private HttpRequest _Request;  
        private WebserverEvents _Events = new WebserverEvents();
        private int _StatusCode = 200;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpResponse()
        {

        }

        internal HttpResponse(
            string ipPort, 
            WebserverSettings.HeaderSettings headers, 
            Stream stream, 
            HttpRequest req, 
            WebserverEvents events, 
            int bufferSize)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (events == null) throw new ArgumentNullException(nameof(events));

            ProtocolVersion = req.ProtocolVersion;

            _IpPort = ipPort;
            _HeaderSettings = headers;
            _Request = req;
            _Stream = stream;
            _Events = events;
            _StreamBufferSize = bufferSize; 
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve a JSON-encoded version of the response object.
        /// </summary>
        /// <param name="pretty">True to enable pretty print.</param>
        /// <returns>JSON string.</returns>
        public string ToJson(bool pretty)
        {
            return SerializationHelper.SerializeJson(this, pretty);
        }

        /// <summary>
        /// Send headers and no data to the requestor and terminate the connection.
        /// </summary> 
        public void Send(bool close)
        { 
            SendInternal(0, null, true); 
        }

        /// <summary>
        /// Try to send headers and no data to the requestor and terminate the connection.
        /// </summary> 
        /// <returns>True if successful.</returns>
        public bool TrySend(bool close)
        {
            try
            {
                Send(close);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers with a specified content length and no data to the requestor and terminate the connection.  Useful for HEAD requests where the content length must be set.
        /// </summary> 
        /// <param name="contentLength">Value to set in Content-Length header.</param>
        public void Send(long contentLength)
        {
            ContentLength = contentLength;
            SendInternal(0, null, true); 
        }

        /// <summary>
        /// Try to send headers with a specified content length and no data to the requestor and terminate the connection.  Useful for HEAD requests where the content length must be set.
        /// </summary> 
        /// <param name="contentLength">Value to set in Content-Length header.</param>
        /// <returns>True if successful.</returns>
        public bool TrySend(long contentLength)
        {
            try
            {
                Send(contentLength);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        public void Send(string data)
        {
            if (String.IsNullOrEmpty(data))
            {
                SendInternal(0, null, true);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            ms.Write(bytes, 0, bytes.Length);
            ms.Seek(0, SeekOrigin.Begin);
            SendInternal(bytes.Length, ms, true);
        }

        /// <summary>
        /// Try to send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        /// <returns>True if successful.</returns>
        public bool TrySend(string data)
        {
            try
            {
                Send(data);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        public void Send(byte[] data)
        {
            if (data == null)
            {
                SendInternal(0, null, true);
                return;
            }
            MemoryStream ms = new MemoryStream();
            ms.Write(data, 0, data.Length);
            ms.Seek(0, SeekOrigin.Begin);
            SendInternal(data.Length, ms, true);
        }

        /// <summary>
        /// Try to send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        /// <returns>True if successful.</returns>
        public bool TrySend(byte[] data)
        {
            try
            {
                Send(data);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="contentLength">Number of bytes to read from the stream.</param>
        /// <param name="stream">Stream containing response data.</param>
        public void Send(long contentLength, Stream stream)
        {
            if (contentLength <= 0 || stream == null || !stream.CanRead)
            {
                SendInternal(0, null, true);
                return;
            }

            SendInternal(contentLength, stream, true);
        }

        /// <summary>
        /// Try to send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="contentLength">Number of bytes to read from the stream.</param>
        /// <param name="stream">Stream containing response data.</param>
        /// <returns>True if successful.</returns>
        public bool TrySend(long contentLength, Stream stream)
        {
            try
            {
                Send(contentLength, stream);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers with a specified content length and no data to the requestor and terminate the connection.  Useful for HEAD requests where the content length must be set.
        /// </summary> 
        /// <param name="contentLength">Value to set in Content-Length header.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(long contentLength, CancellationToken token = default)
        {
            ContentLength = contentLength;
            await SendInternalAsync(0, null, true, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Try to send headers with a specified content length and no data to the requestor and terminate the connection.  Useful for HEAD requests where the content length must be set.
        /// </summary> 
        /// <param name="contentLength">Value to set in Content-Length header.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> TrySendAsync(long contentLength, CancellationToken token = default)
        {
            try
            {
                await SendAsync(contentLength, token);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data))
            {
                await SendInternalAsync(0, null, true, token).ConfigureAwait(false);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            await SendInternalAsync(bytes.Length, ms, true, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Try to send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        /// <param name="token">Cancellation token for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> TrySendAsync(string data, CancellationToken token = default)
        {
            try
            {
                await SendAsync(data, token);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(byte[] data, CancellationToken token = default)
        {
            if (data == null || data.Length < 1)
            {
                await SendInternalAsync(0, null, true, token).ConfigureAwait(false);
                return;
            } 
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin); 
            await SendInternalAsync(data.Length, ms, true, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Try to send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        /// <param name="token">Cancellation token for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> TrySendAsync(byte[] data, CancellationToken token = default)
        {
            try
            {
                await SendAsync(data, token);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="contentLength">Number of bytes to read from the stream.</param>
        /// <param name="stream">Stream containing response data.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(long contentLength, Stream stream, CancellationToken token = default)
        {
            if (contentLength <= 0 || stream == null || !stream.CanRead)
            {
                await SendInternalAsync(0, null, true, token).ConfigureAwait(false);
                return;
            }

            await SendInternalAsync(contentLength, stream, true, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Try to send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="contentLength">Number of bytes to read from the stream.</param>
        /// <param name="stream">Stream containing response data.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> TrySendAsync(long contentLength, Stream stream, CancellationToken token = default)
        {
            try
            {
                await SendAsync(contentLength, stream, token);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="contentLength">Value to set in Content-Length header.</param>
        public void SendWithoutClose(long contentLength)
        {
            ContentLength = contentLength;
            SendInternal(contentLength, null, false);
        }

        /// <summary>
        /// Try to send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="contentLength">Value to set in Content-Length header.</param>
        /// <returns>True if successful.</returns>
        public bool TrySendWithoutClose(long contentLength)
        {
            try
            {
                SendWithoutClose(contentLength);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        public void SendWithoutClose(string data)
        {
            if (String.IsNullOrEmpty(data))
            {
                SendInternal(0, null, false);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            ms.Write(bytes, 0, bytes.Length);
            ms.Seek(0, SeekOrigin.Begin);
            SendInternal(bytes.Length, ms, false);
        }

        /// <summary>
        /// Try to send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        /// <returns>True if successful.</returns>
        public bool TrySendWithoutClose(string data)
        {
            try
            {
                SendWithoutClose(data);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        public void SendWithoutClose(byte[] data)
        {
            if (data == null)
            {
                SendInternal(0, null, false);
                return;
            }
            MemoryStream ms = new MemoryStream();
            ms.Write(data, 0, data.Length);
            ms.Seek(0, SeekOrigin.Begin);
            SendInternal(data.Length, ms, false);
        }

        /// <summary>
        /// Try to send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        /// <returns>True if successful.</returns>
        public bool TrySendWithoutClose(byte[] data)
        {
            try
            {
                SendWithoutClose(data);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="contentLength">Number of bytes to read from the stream.</param>
        /// <param name="stream">Stream containing response data.</param>
        public void SendWithoutClose(long contentLength, Stream stream)
        {
            if (contentLength <= 0 || stream == null || !stream.CanRead)
            {
                SendInternal(0, null, false);
                return;
            }

            SendInternal(contentLength, stream, false);
        }

        /// <summary>
        /// Try to send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="contentLength">Number of bytes to read from the stream.</param>
        /// <param name="stream">Stream containing response data.</param>
        /// <returns>True if successful.</returns>
        public bool TrySendWithoutClose(long contentLength, Stream stream)
        {
            try
            {
                SendWithoutClose(contentLength, stream);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="contentLength">Value to set in Content-Length header.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendWithoutCloseAsync(long contentLength, CancellationToken token = default)
        {
            ContentLength = contentLength;
            await SendInternalAsync(contentLength, null, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Try to send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="contentLength">Value to set in Content-Length header.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> TrySendWithoutCloseAsync(long contentLength, CancellationToken token = default)
        {
            try
            {
                await SendWithoutCloseAsync(contentLength, token);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendWithoutCloseAsync(string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data))
            {
                await SendInternalAsync(0, null, false, token).ConfigureAwait(false);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            await SendInternalAsync(bytes.Length, ms, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Try to send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        /// <param name="token">Cancellation token for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> TrySendWithoutCloseAsync(string data, CancellationToken token = default)
        {
            try
            {
                await SendWithoutCloseAsync(data, token);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendWithoutCloseAsync(byte[] data, CancellationToken token = default)
        {
            if (data == null)
            {
                await SendInternalAsync(0, null, false, token).ConfigureAwait(false);
                return;
            }
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            await SendInternalAsync(data.Length, ms, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Try to send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        /// <param name="token">Cancellation token for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> TrySendWithoutCloseAsync(byte[] data, CancellationToken token = default)
        {
            try
            {
                await SendWithoutCloseAsync(data, token);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="contentLength">Number of bytes to read from the stream.</param>
        /// <param name="stream">Stream containing response data.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendWithoutCloseAsync(long contentLength, Stream stream, CancellationToken token = default)
        {
            if (contentLength <= 0 || stream == null || !stream.CanRead)
            {
                await SendInternalAsync(0, null, false, token).ConfigureAwait(false);
                return;
            }

            await SendInternalAsync(contentLength, stream, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Try to send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="contentLength">Number of bytes to read from the stream.</param>
        /// <param name="stream">Stream containing response data.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> TrySendWithoutCloseAsync(long contentLength, Stream stream, CancellationToken token = default)
        {
            try
            {
                await SendWithoutCloseAsync(contentLength, stream, token);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Close the connection.
        /// </summary>
        public void Close()
        {
            SendInternal(0, null, true);
        }

        #endregion

        #region Private-Methods

        private byte[] GetHeaderBytes()
        {
            StatusDescription = GetStatusDescription();

            byte[] ret = new byte[0];

            ret = Common.AppendBytes(ret, Encoding.UTF8.GetBytes(ProtocolVersion + " " + StatusCode + " " + StatusDescription + "\r\n"));

            bool contentTypeSet = false;
            if (!String.IsNullOrEmpty(ContentType))
            {
                ret = Common.AppendBytes(ret, Encoding.UTF8.GetBytes("Content-Type: " + ContentType + "\r\n"));
                contentTypeSet = true;
            }

            bool contentLengthSet = false;
            if (ContentLength != null && ContentLength >= 0)
            {
                ret = Common.AppendBytes(ret, Encoding.UTF8.GetBytes("Content-Length: " + ContentLength + "\r\n"));
                contentLengthSet = true;
            }

            for (int i = 0; i < _Headers.Count; i++)
            {
                string header = _Headers.GetKey(i);
                if (String.IsNullOrEmpty(header)) continue;
                if (contentTypeSet && header.ToLower().Equals("content-type")) continue;
                if (contentLengthSet && header.ToLower().Equals("content-length")) continue;
                string[] vals = _Headers.GetValues(i);
                if (vals != null && vals.Length > 0)
                {
                    foreach (string val in vals)
                    {
                        ret = Common.AppendBytes(ret, Encoding.UTF8.GetBytes(header + ": " + val + "\r\n"));
                    }
                }
            }

            ret = Common.AppendBytes(ret, Encoding.UTF8.GetBytes("\r\n"));
            return ret;
        }
         
        private string GetStatusDescription()
        {
            switch (StatusCode)
            {
                case 200:
                    return "OK";
                case 201:
                    return "Created";
                case 301:
                    return "Moved Permanently";
                case 302:
                    return "Moved Temporarily";
                case 304:
                    return "Not Modified";
                case 400:
                    return "Bad Request";
                case 401:
                    return "Unauthorized";
                case 403:
                    return "Forbidden";
                case 404:
                    return "Not Found";
                case 405:
                    return "Method Not Allowed";
                case 429:
                    return "Too Many Requests";
                case 500:
                    return "Internal Server Error";
                case 501:
                    return "Not Implemented";
                case 503:
                    return "Service Unavailable";
                default:
                    return "Unknown";
            }
        }

        private void SetDefaultHeaders()
        {
            if (_HeaderSettings != null && _Headers != null)
            {
                bool accessControlAllowOriginSet = false;
                bool accessControlAllowMethodsSet = false;
                bool accessControlAllowHeadersSet = false;
                bool accessControlExposeHeadersSet = false;
                bool acceptSet = false;
                bool acceptLanguageSet = false;
                bool acceptCharsetSet = false;
                bool connectionSet = false;
                bool hostSet = false;

                for (int i = 0; i < _Headers.Count; i++)
                {
                    string key = _Headers.GetKey(i);

                    if (!String.IsNullOrEmpty(_HeaderSettings.AccessControlAllowOrigin) && key.Equals("access-control-allow-origin"))
                        accessControlAllowOriginSet = true;

                    if (!String.IsNullOrEmpty(_HeaderSettings.AccessControlAllowMethods) && key.Equals("access-control-allow-methods"))
                        accessControlAllowMethodsSet = true;

                    if (!String.IsNullOrEmpty(_HeaderSettings.AccessControlAllowHeaders) && key.Equals("access-control-allow-headers"))
                        accessControlAllowHeadersSet = true;

                    if (!String.IsNullOrEmpty(_HeaderSettings.AccessControlAllowOrigin) && key.Equals("access-control-expose-headers"))
                        accessControlExposeHeadersSet = true;

                    if (!String.IsNullOrEmpty(_HeaderSettings.Accept) && key.Equals("accept"))
                        acceptSet = true;

                    if (!String.IsNullOrEmpty(_HeaderSettings.AcceptLanguage) && key.Equals("accept-language"))
                        acceptLanguageSet = true;

                    if (!String.IsNullOrEmpty(_HeaderSettings.AcceptCharset) && key.Equals("accept-charset"))
                        acceptCharsetSet = true;

                    if (!String.IsNullOrEmpty(_HeaderSettings.Connection) && key.Equals("connection"))
                        connectionSet = true;

                    if (!String.IsNullOrEmpty(_HeaderSettings.Host) && key.Equals("host"))
                        hostSet = true;

                }

                if (!accessControlAllowOriginSet && !String.IsNullOrEmpty(_HeaderSettings.AccessControlAllowOrigin))
                    _Headers.Add("Access-Control-Allow-Origin", _HeaderSettings.AccessControlAllowOrigin);

                if (!accessControlAllowMethodsSet && !String.IsNullOrEmpty(_HeaderSettings.AccessControlAllowMethods))
                    _Headers.Add("Access-Control-Allow-Methods", _HeaderSettings.AccessControlAllowMethods);

                if (!accessControlAllowHeadersSet && !String.IsNullOrEmpty(_HeaderSettings.AccessControlAllowHeaders))
                    _Headers.Add("Access-Control-Allow-Headers", _HeaderSettings.AccessControlAllowHeaders);

                if (!accessControlExposeHeadersSet && !String.IsNullOrEmpty(_HeaderSettings.AccessControlExposeHeaders))
                    _Headers.Add("Access-Control-Expose-Headers", _HeaderSettings.AccessControlExposeHeaders);

                if (!acceptSet && !String.IsNullOrEmpty(_HeaderSettings.Accept))
                    _Headers.Add("Accept", _HeaderSettings.Accept);

                if (!acceptLanguageSet && !String.IsNullOrEmpty(_HeaderSettings.AcceptLanguage))
                    _Headers.Add("Accept-Language", _HeaderSettings.AcceptLanguage);

                if (!acceptCharsetSet && !String.IsNullOrEmpty(_HeaderSettings.AcceptCharset))
                    _Headers.Add("Accept-Charset", _HeaderSettings.AcceptCharset);

                if (!connectionSet && !String.IsNullOrEmpty(_HeaderSettings.Connection))
                    _Headers.Add("Connection", _HeaderSettings.Connection);

                if (!hostSet && !String.IsNullOrEmpty(_HeaderSettings.Host))
                    _Headers.Add("Host", _HeaderSettings.Host);
            }
        }

        private void SetContentLength(long contentLength)
        {
            if (_HeaderSettings.IncludeContentLength)
            {
                if (_Headers.Count > 0)
                {
                    for (int i = 0; i < _Headers.Count; i++)
                    {
                        string val = _Headers.GetKey(i);
                        if (!String.IsNullOrEmpty(val)
                            && val.ToLower().Equals("content-length"))
                        {
                            return;
                        }
                    }
                }

                _Headers.Add("Content-Length", contentLength.ToString());
            }
        }

        private void SendInternal(long contentLength, Stream stream, bool close)
        {
            if (!HeadersSent)
            {
                SetDefaultHeaders();
                SetContentLength(contentLength);
                byte[] headers = GetHeaderBytes();
                _Stream.Write(headers, 0, headers.Length);
                _Stream.Flush();
                HeadersSent = true;
            }

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                long bytesRemaining = contentLength;

                byte[] buffer = null;
                while (bytesRemaining > 0)
                {
                    if (bytesRemaining >= _StreamBufferSize) buffer = new byte[_StreamBufferSize];
                    else buffer = new byte[contentLength];

                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        _Stream.Write(buffer, 0, buffer.Length);
                        bytesRemaining -= bytesRead;
                    }
                }

                _Stream.Flush();
            }

            if (close)
            {
                _Stream.Close(); 
            }
        }

        private async Task SendInternalAsync(long contentLength, Stream stream, bool close, CancellationToken token)
        { 
            byte[] resp = new byte[0];
            if (!HeadersSent)
            {
                SetDefaultHeaders();
                SetContentLength(contentLength);
                byte[] headers = GetHeaderBytes(); 
                await _Stream.WriteAsync(headers, 0, headers.Length, token).ConfigureAwait(false);
                await _Stream.FlushAsync(token).ConfigureAwait(false);
                HeadersSent = true;
            }

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                long bytesRemaining = contentLength;

                byte[] buffer = new byte[_StreamBufferSize];
                int bytesToRead = _StreamBufferSize;
                int bytesRead = 0;

                while (bytesRemaining > 0)
                {
                    if (bytesRemaining > _StreamBufferSize) bytesToRead = _StreamBufferSize;
                    else bytesToRead = (int)bytesRemaining;

                    bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead, token).ConfigureAwait(false);
                    if (bytesRead > 0)
                    { 
                        await _Stream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                        bytesRemaining -= bytesRead;
                    }
                }
                 
                await _Stream.FlushAsync(token).ConfigureAwait(false);
            }

            if (close)
            { 
                _Stream.Close();
            }
        }

        #endregion
    }
}
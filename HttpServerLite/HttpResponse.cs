using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

        /// <summary>
        /// The protocol and version.
        /// </summary>
        public string ProtocolVersion;

        /// <summary>
        /// The HTTP status code to return to the requestor (client).
        /// </summary>
        public int StatusCode = 200;

        /// <summary>
        /// The HTTP status description to return to the requestor (client).
        /// </summary>
        public string StatusDescription = "OK";

        /// <summary>
        /// User-supplied headers to include in the response.
        /// </summary>
        public Dictionary<string, string> Headers
        {
            get
            {
                return _Headers;
            }
            set
            {
                if (value == null) _Headers = new Dictionary<string, string>();
                else _Headers = value;
            }
        }

        /// <summary>
        /// User-supplied content-type to include in the response.
        /// </summary>
        public string ContentType = String.Empty;

        /// <summary>
        /// The length of the supplied response data.
        /// </summary>
        public long? ContentLength = null;

        /// <summary>
        /// Access-Control-Allow-Origin header value.
        /// </summary>
        public string AccessControlAllowOriginHeader = "*";

        #endregion

        #region Internal-Members

        internal bool ResponseSent = false; 
        internal bool HeadersSent = false;

        #endregion

        #region Private-Members

        private string _IpPort;
        private int _StreamBufferSize = 65536;
        private Dictionary<string, string> _Headers = new Dictionary<string, string>();
        private Stream _Stream;
        private HttpRequest _Request;  
        private EventCallbacks _Events = new EventCallbacks();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpResponse()
        {

        }

        internal HttpResponse(string ipPort, Stream stream, HttpRequest req, EventCallbacks events, int bufferSize)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (events == null) throw new ArgumentNullException(nameof(events));

            ProtocolVersion = req.ProtocolVersion;

            _IpPort = ipPort;
            _Request = req;
            _Stream = stream;
            _Events = events;
            _StreamBufferSize = bufferSize; 
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve a string-formatted, human-readable copy of the HttpResponse instance.
        /// </summary>
        /// <returns>String-formatted, human-readable copy of the HttpResponse instance.</returns>
        public override string ToString()
        {
            string ret = "";

            ret += "--- HTTP Response ---" + Environment.NewLine; 
            ret += "  Status Code        : " + StatusCode + Environment.NewLine;
            ret += "  Status Description : " + StatusDescription + Environment.NewLine;
            ret += "  Content            : " + ContentType + Environment.NewLine;
            ret += "  Content Length     : " + ContentLength + " bytes" + Environment.NewLine; 
            if (Headers != null && Headers.Count > 0)
            {
                ret += "  Headers            : " + Environment.NewLine;
                foreach (KeyValuePair<string, string> curr in Headers)
                {
                    ret += "  - " + curr.Key + ": " + curr.Value + Environment.NewLine;
                }
            }
            else
            {
                ret += "  Headers          : none" + Environment.NewLine;
            }

            return ret;
        }

        /// <summary>
        /// Send headers and no data to the requestor and terminate the connection.
        /// </summary> 
        public void Send(bool close)
        { 
            SendInternal(0, null, true); 
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
        /// Send headers with a specified content length and no data to the requestor and terminate the connection.  Useful for HEAD requests where the content length must be set.
        /// </summary> 
        /// <param name="contentLength">Value to set in Content-Length header.</param>
        public async Task SendAsync(long contentLength)
        {
            ContentLength = contentLength;
            await SendInternalAsync(0, null, true);
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        public async Task SendAsync(string data)
        {
            if (String.IsNullOrEmpty(data))
            {
                await SendInternalAsync(0, null, true);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(bytes, 0, bytes.Length);
            ms.Seek(0, SeekOrigin.Begin);
            await SendInternalAsync(bytes.Length, ms, true);
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        public async Task SendAsync(byte[] data)
        {
            if (data == null || data.Length < 1)
            {
                await SendInternalAsync(0, null, true);
                return;
            } 
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(data, 0, data.Length);
            ms.Seek(0, SeekOrigin.Begin); 
            await SendInternalAsync(data.Length, ms, true);
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="contentLength">Number of bytes to read from the stream.</param>
        /// <param name="stream">Stream containing response data.</param>
        public async Task SendAsync(long contentLength, Stream stream)
        {
            if (contentLength <= 0 || stream == null || !stream.CanRead)
            {
                await SendInternalAsync(0, null, true);
                return;
            }

            await SendInternalAsync(contentLength, stream, true);
        }

        /// <summary>
        /// Send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="contentLength">Value to set in Content-Length header.</param>
        public void SendWithoutClose(long contentLength)
        {
            ContentLength = contentLength;
            SendInternal(0, null, false);
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
        /// Send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        public async Task SendWithoutCloseAsync(string data)
        {
            if (String.IsNullOrEmpty(data))
            {
                await SendInternalAsync(0, null, false);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(bytes, 0, bytes.Length);
            ms.Seek(0, SeekOrigin.Begin);
            await SendInternalAsync(bytes.Length, ms, false);
        }

        /// <summary>
        /// Send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        public async Task SendWithoutCloseAsync(byte[] data)
        {
            if (data == null)
            {
                await SendInternalAsync(0, null, false);
                return;
            }
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(data, 0, data.Length);
            ms.Seek(0, SeekOrigin.Begin);
            await SendInternalAsync(data.Length, ms, false);
        }

        /// <summary>
        /// Send headers and data to the requestor but do not terminate the connection.
        /// </summary>
        /// <param name="contentLength">Number of bytes to read from the stream.</param>
        /// <param name="stream">Stream containing response data.</param>
        public async Task SendWithoutCloseAsync(long contentLength, Stream stream)
        {
            if (contentLength <= 0 || stream == null || !stream.CanRead)
            {
                await SendInternalAsync(0, null, false);
                return;
            }

            await SendInternalAsync(contentLength, stream, false);
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
            ret = Common.AppendBytes(ret, Encoding.UTF8.GetBytes("Access-Control-Allow-Origin: " + AccessControlAllowOriginHeader + "\r\n"));

            if (!String.IsNullOrEmpty(ContentType))
                ret = Common.AppendBytes(ret, Encoding.UTF8.GetBytes("Content-Type: " + ContentType + "\r\n"));

            if (ContentLength != null && ContentLength >= 0)
                ret = Common.AppendBytes(ret, Encoding.UTF8.GetBytes("Content-Length: " + ContentLength + "\r\n"));
             
            foreach (KeyValuePair<string, string> header in _Headers)
                ret = Common.AppendBytes(ret, Encoding.UTF8.GetBytes(header.Key + ": " + header.Value + "\r\n"));

            ret = Common.AppendBytes(ret, Encoding.UTF8.GetBytes("\r\n"));
            return ret;
        }

        private void SendHeaders()
        {
            if (HeadersSent) throw new IOException("Headers already sent.");
            StatusDescription = GetStatusDescription();
            byte[] headers = GetHeaderBytes();
            _Stream.Write(headers, 0, headers.Length);
            HeadersSent = true;
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
          
        private void SendInternal(long contentLength, Stream stream, bool close)
        {   
            byte[] resp = new byte[0];
            if (!HeadersSent)
            {
                byte[] headers = GetHeaderBytes();
                _Stream.Write(headers, 0, headers.Length);
                _Stream.Flush();
                HeadersSent = true;
            }

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                long bytesRemaining = contentLength;

                while (bytesRemaining > 0)
                {
                    byte[] buffer = null;
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

        private async Task SendInternalAsync(long contentLength, Stream stream, bool close)
        { 
            byte[] resp = new byte[0];
            if (!HeadersSent)
            {
                byte[] headers = GetHeaderBytes(); 
                await _Stream.WriteAsync(headers, 0, headers.Length);
                await _Stream.FlushAsync();
                HeadersSent = true;
            }

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                long bytesRemaining = contentLength;

                while (bytesRemaining > 0)
                {
                    byte[] buffer = null;
                    if (bytesRemaining >= _StreamBufferSize) buffer = new byte[_StreamBufferSize];
                    else buffer = new byte[contentLength];

                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    { 
                        await _Stream.WriteAsync(buffer, 0, bytesRead);
                        bytesRemaining -= bytesRead;
                    }
                }
                 
                await _Stream.FlushAsync();
            }

            if (close)
            { 
                _Stream.Close();
            }
        }

        #endregion
    }
}
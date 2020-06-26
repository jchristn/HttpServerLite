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
         
        #endregion

        #region Internal-Members

        internal bool ResponseSent
        {
            get
            {
                return _ResponseSent;
            }
        }

        internal bool HeadersSent = false;

        #endregion

        #region Private-Members

        private string _IpPort;
        private int _StreamBufferSize = 65536;
        private Dictionary<string, string> _Headers = new Dictionary<string, string>();
        private TcpServer _Tcp;
        private HttpRequest _Request; 
        private bool _ResponseSent = false; 
        private EventCallbacks _Events = new EventCallbacks();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpResponse()
        {

        }

        internal HttpResponse(string ipPort, HttpRequest req, TcpServer server, EventCallbacks events, int bufferSize)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (events == null) throw new ArgumentNullException(nameof(events));

            _IpPort = ipPort;
            _Request = req;
            _Tcp = server;
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
        public void Send()
        { 
            SendInternal(null, true); 
        }

        /// <summary>
        /// Send headers with a specified content length and no data to the requestor and terminate the connection.  Useful for HEAD requests where the content length must be set.
        /// </summary> 
        public void Send(long contentLength)
        {  
            SendInternal(null, true); 
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        public void Send(string data)
        { 
            if (String.IsNullOrEmpty(data)) SendInternal(null, true);
            byte[] bytes = Encoding.UTF8.GetBytes(data); 
            SendInternal(bytes, true); 
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param> 
        public void Send(byte[] data)
        {
            if (data == null) SendInternal(data, true); 
            SendInternal(data, true); 
        }
          
        #endregion

        #region Private-Methods

        private byte[] GetHeaderBytes()
        {
            byte[] ret = new byte[0];

            ret = Common.AppendBytes(ret, Encoding.UTF8.GetBytes("HTTP/" + _Request.ProtocolVersion + " " + StatusCode + " " + StatusDescription + "\r\n"));
            ret = Common.AppendBytes(ret, Encoding.UTF8.GetBytes("Access-Control-Allow-Origin: *\r\n"));

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
            byte[] headers = GetHeaderBytes();
            Console.WriteLine("SendHeaders: " + headers.Length + " bytes");
            _Tcp.Send(_IpPort, headers); 
            HeadersSent = true;
        }

        private string GetStatusDescription(int statusCode)
        {
            switch (statusCode)
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
                    return "Unknown Status";
            }
        }
         
        private void SendInternal(byte[] data, bool close)
        {
            try
            {
                byte[] resp = new byte[0];
                if (!HeadersSent)
                {
                    byte[] headers = GetHeaderBytes();
                    Console.WriteLine("[SendInternal] appending headers: " + headers.Length + " bytes");
                    resp = Common.AppendBytes(resp, headers);
                    HeadersSent = true;
                }

                if (data != null && data.Length > 0)
                {
                    Console.WriteLine("[SendInternal] appending data: " + data.Length + " bytes");
                    resp = Common.AppendBytes(resp, data);
                }

                Console.WriteLine("[SendInternal] sending " + resp.Length + " bytes: " + Environment.NewLine + Encoding.UTF8.GetString(resp));
                _Tcp.Send(_IpPort, resp);

                if (close)
                {
                    Console.WriteLine("[SendInternal] closing");
                    _Tcp.DisconnectClient(_IpPort);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(SerializationHelper.SerializeJson(e, true));
            }
        }

        #endregion
    }
}
using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CavemanTcp;
using Timestamps;

namespace HttpServerLite
{
    /// <summary>
    /// Data extracted from an incoming HTTP request.
    /// </summary>
    public class HttpRequest
    {
        #region Public-Members

        /// <summary>
        /// UTC timestamp from when the request was received.
        /// </summary>
        [JsonPropertyOrder(-8)]
        public DateTime TimestampUtc { get; private set; } = DateTime.Now.ToUniversalTime();

        /// <summary>
        /// Thread ID on which the request exists.
        /// </summary>
        [JsonPropertyOrder(-7)]
        public int ThreadId { get; private set; } = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// The protocol and version.
        /// </summary>
        [JsonPropertyOrder(-6)]
        public string ProtocolVersion { get; set; } = null;

        /// <summary>
        /// Source (requestor) IP and port information.
        /// </summary>
        [JsonPropertyOrder(-5)]
        public SourceDetails Source { get; set; } = new SourceDetails();

        /// <summary>
        /// The HTTP method used in the request.
        /// </summary>
        [JsonPropertyOrder(-4)]
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// URL details.
        /// </summary>
        [JsonPropertyOrder(-3)]
        public UrlDetails Url { get; set; } = new UrlDetails();

        /// <summary>
        /// Query details.
        /// </summary>
        [JsonPropertyOrder(-2)]
        public QueryDetails Query { get; set; } = new QueryDetails();

        /// <summary>
        /// The headers found in the request.
        /// </summary>
        [JsonPropertyOrder(-1)]
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
        /// Specifies whether or not the client requested HTTP keepalives.
        /// </summary>
        public bool Keepalive { get; set; } = false;

        /// <summary>
        /// Indicates whether or not chunked transfer encoding was detected.
        /// </summary>
        public bool ChunkedTransfer { get; set; } = false;
 
        /// <summary>
        /// Indicates whether or not the payload has been gzip compressed.
        /// </summary>
        public bool Gzip { get; set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been deflate compressed.
        /// </summary>
        public bool Deflate { get; set; } = false;

        /// <summary>
        /// The useragent specified in the request.
        /// </summary>
        public string Useragent { get; set; } = null;

        /// <summary>
        /// The content type as specified by the requestor (client).
        /// </summary>
        [JsonPropertyOrder(990)]
        public string ContentType { get; set; } = null;

        /// <summary>
        /// The number of bytes in the request body.
        /// </summary>
        [JsonPropertyOrder(991)]
        public long ContentLength { get; private set; } = 0;

        /// <summary>
        /// The stream containing request data.
        /// </summary>
        [JsonIgnore]
        public Stream Data;

        /// <summary>
        /// Bytes from the DataStream property.  Using Data will fully read the DataStream property and thus it cannot be read again.
        /// </summary>
        public byte[] DataAsBytes
        {
            get
            {
                if (_DataAsBytes == null)
                {
                    if (Data != null && Data.CanRead && ContentLength > 0)
                    {
                        _DataAsBytes = Common.ReadStream(_StreamBufferSize, ContentLength, Data);
                        return _DataAsBytes;
                    }
                    else
                    {
                        return _DataAsBytes;
                    }
                }
                else
                {
                    return _DataAsBytes;
                }
            }
        }

        /// <summary>
        /// Retrieve the request body as a string.  This will fully read the stream.
        /// </summary>
        [JsonIgnore]
        public string DataAsString
        {
            get
            {
                if (_DataAsBytes != null) return Encoding.UTF8.GetString(_DataAsBytes);
                if (Data != null && ContentLength > 0)
                {
                    _DataAsBytes = ReadStream(Data, ContentLength);
                    if (_DataAsBytes != null) return Encoding.UTF8.GetString(_DataAsBytes);
                }
                return null;
            }
        }

        #endregion

        #region Private-Members

        private int _StreamBufferSize = 65536;
        private string _IpPort;
        private string _RequestHeader = null;  
        private byte[] _DataAsBytes = null; 
        private NameValueCollection _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpRequest()
        {
            ThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Create an HttpRequest.
        /// </summary>
        /// <param name="ipPort">IP:port of the requestor.</param>
        /// <param name="stream">Client stream.</param>
        /// <param name="requestHeader">Request header.</param>
        /// <returns>HttpRequest.</returns>
        public HttpRequest(string ipPort, Stream stream, string requestHeader)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (String.IsNullOrEmpty(requestHeader)) throw new ArgumentNullException(nameof(requestHeader));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            _IpPort = ipPort;
            _RequestHeader = requestHeader;
            Data = stream;

            Build();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve a JSON-encoded version of the request object.
        /// </summary>
        /// <param name="pretty">True to enable pretty print.</param>
        /// <returns>JSON string.</returns>
        public string ToJson(bool pretty)
        {
            return SerializationHelper.SerializeJson(this, pretty);
        }

        /// <summary>
        /// For chunked transfer-encoded requests, read the next chunk.
        /// It is strongly recommended that you use the ChunkedTransfer parameter before invoking this method.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>Chunk.</returns>
        public async Task<Chunk> ReadChunk(CancellationToken token = default)
        {
            Chunk chunk = new Chunk();

            #region Get-Length-and-Metadata

            byte[] buffer = new byte[1];
            byte[] lenBytes = null;
            int bytesRead = 0;

            while (true)
            {
                bytesRead = await Data.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (bytesRead > 0)
                {
                    lenBytes = AppendBytes(lenBytes, buffer);
                    string lenStr = Encoding.UTF8.GetString(lenBytes);

                    if (lenBytes[lenBytes.Length - 1] == 10)
                    {
                        lenStr = lenStr.Trim();

                        if (lenStr.Contains(";"))
                        {
                            string[] lenParts = lenStr.Split(new char[] { ';' }, 2);
                            chunk.Length = int.Parse(lenParts[0], NumberStyles.HexNumber);
                            if (lenParts.Length >= 2) chunk.Metadata = lenParts[1];
                        }
                        else
                        {
                            chunk.Length = int.Parse(lenStr, NumberStyles.HexNumber);
                        }

                        break;
                    }
                }
            }

            #endregion

            #region Get-Data

            int bytesRemaining = chunk.Length;

            if (chunk.Length > 0)
            {
                chunk.IsFinal = false;
                using (MemoryStream ms = new MemoryStream())
                {
                    while (true)
                    {
                        if (bytesRemaining > _StreamBufferSize) buffer = new byte[_StreamBufferSize];
                        else buffer = new byte[bytesRemaining];

                        bytesRead = await Data.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);

                        if (bytesRead > 0)
                        {
                            await ms.WriteAsync(buffer, 0, bytesRead);
                            bytesRemaining -= bytesRead;
                        }

                        if (bytesRemaining == 0) break;
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    chunk.Data = ms.ToArray();
                }
            }
            else
            {
                chunk.IsFinal = true;
            }

            #endregion

            #region Get-Trailing-CRLF

            buffer = new byte[1];

            while (true)
            {
                bytesRead = await Data.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (bytesRead > 0)
                {
                    if (buffer[0] == 10) break;
                }
            }

            #endregion

            return chunk;
        }

        /// <summary>
        /// Read the data stream fully and convert the data to the object type specified using JSON deserialization.
        /// Note: if you use this method, you will not be able to read from the data stream afterward.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <returns>Object of type specified.</returns>
        public T DataAsJsonObject<T>()where T : class
        {
            if (String.IsNullOrEmpty(DataAsString)) return null;
            return SerializationHelper.DeserializeJson<T>(DataAsString);
        }

        /// <summary>
        /// Determine if a header exists.
        /// </summary>
        /// <param name="key">Header key.</param>
        /// <returns>True if exists.</returns>
        public bool HeaderExists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (Headers != null)
            {
                return Headers.AllKeys.Any(k => k.ToLower().Equals(key.ToLower()));
            }

            return false;
        }

        /// <summary>
        /// Determine if a querystring entry exists.
        /// </summary>
        /// <param name="key">Querystring key.</param>
        /// <returns>True if exists.</returns>
        public bool QuerystringExists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (Query != null
                && Query.Elements != null)
            {
                return Query.Elements.AllKeys.Any(k => k.ToLower().Equals(key.ToLower()));
            }

            return false;
        }

        /// <summary>
        /// Retrieve a header (or querystring) value.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Value.</returns>
        public string RetrieveHeaderValue(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (Headers != null)
            {
                return Headers.Get(key);
            }

            return null;
        }

        /// <summary>
        /// Retrieve a querystring value.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Value.</returns>
        public string RetrieveQueryValue(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (Query != null
                && Query.Elements != null)
            {
                string val = Query.Elements.Get(key);
                if (!String.IsNullOrEmpty(val))
                {
                    val = WebUtility.UrlDecode(val);
                }

                return val;
            }

            return null;
        }

        #endregion

        #region Private-Methods

        private void Build()
        { 
            #region Initial-Values

            TimestampUtc = DateTime.Now.ToUniversalTime();
            Source = new SourceDetails(Common.IpFromIpPort(_IpPort), Common.PortFromIpPort(_IpPort));
            ThreadId = Thread.CurrentThread.ManagedThreadId; 
             
            #endregion

            #region Convert-to-String-List
             
            string[] headers = _RequestHeader.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            #endregion

            #region Process-Each-Line

            for (int i = 0; i < headers.Length; i++)
            {
                if (i == 0)
                {
                    #region First-Line

                    string[] requestLine = headers[i].Trim().Trim('\0').Split(' ');
                    if (requestLine.Length < 3) throw new ArgumentException("Request line does not contain at least three parts (method, raw URL, protocol/version).");
                    
                    string tempUrl = requestLine[1];
                    string tempPath = "";
                    if (tempUrl.ToLower().StartsWith("http"))
                    {
                        // absolute path
                        var modifiedUri = new UriBuilder(tempUrl);
                        tempPath = modifiedUri.Path;
                    }
                    else
                    {
                        // relative path
                        tempPath = tempUrl;
                    }

                    Method = (HttpMethod)Enum.Parse(typeof(HttpMethod), requestLine[0], true); 
                    Url = new UrlDetails(tempPath);
                    Query = new QueryDetails(requestLine[1]);
                     
                    ProtocolVersion = requestLine[2]; 
                     
                    #endregion
                }
                else
                {
                    #region Subsequent-Line

                    string[] headerLine = headers[i].Split(':');
                    if (headerLine.Length == 2)
                    {
                        string key = headerLine[0].Trim();
                        string val = headerLine[1].Trim();

                        if (String.IsNullOrEmpty(key)) continue;
                        string keyEval = key.ToLower();

                        if (keyEval.Equals("keep-alive"))
                        {
                            Keepalive = Convert.ToBoolean(val);
                        }
                        else if (keyEval.Equals("user-agent"))
                        {
                            Useragent = val;
                        }
                        else if (keyEval.Equals("content-length"))
                        {
                            ContentLength = Convert.ToInt32(val);
                        }
                        else if (keyEval.Equals("content-type"))
                        {
                            ContentType = val;
                        }
                        else if (keyEval.ToLower().Equals("x-amz-content-sha256"))
                        {
                            if (val.ToLower().Contains("streaming"))
                            {
                                ChunkedTransfer = true;
                            }
                        }

                        Headers.Add(key, val);
                    }

                    #endregion
                }
            }

            #endregion
        }
         
        private byte[] AppendBytes(byte[] orig, byte[] append)
        {
            if (orig == null && append == null) return null;

            byte[] ret = null;

            if (append == null)
            {
                ret = new byte[orig.Length];
                Buffer.BlockCopy(orig, 0, ret, 0, orig.Length);
                return ret;
            }

            if (orig == null)
            {
                ret = new byte[append.Length];
                Buffer.BlockCopy(append, 0, ret, 0, append.Length);
                return ret;
            }

            ret = new byte[orig.Length + append.Length];
            Buffer.BlockCopy(orig, 0, ret, 0, orig.Length);
            Buffer.BlockCopy(append, 0, ret, orig.Length, append.Length);
            return ret;
        }

        private byte[] ReadStream(Stream input, long contentLength)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");
            if (contentLength < 1) return new byte[0];

            byte[] buffer = new byte[16 * 1024];
            long bytesRemaining = contentLength;

            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while (bytesRemaining > 0)
                {
                    read = input.Read(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        ms.Write(buffer, 0, read);
                        bytesRemaining -= read;
                    }
                }

                byte[] ret = ms.ToArray();
                return ret;
            }
        }

        #endregion

        #region Public-Classes

        /// <summary>
        /// Source details.
        /// </summary>
        public class SourceDetails
        {
            /// <summary>
            /// IP address of the requestor.
            /// </summary>
            public string IpAddress { get; set; } = null;

            /// <summary>
            /// TCP port from which the request originated on the requestor.
            /// </summary>
            public int Port { get; set; } = 0;

            /// <summary>
            /// Source details.
            /// </summary>
            public SourceDetails()
            {

            }

            /// <summary>
            /// Source details.
            /// </summary>
            /// <param name="ip">IP address of the requestor.</param>
            /// <param name="port">TCP port from which the request originated on the requestor.</param>
            public SourceDetails(string ip, int port)
            {
                if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
                if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));

                IpAddress = ip;
                Port = port;
            }
        }

        /// <summary>
        /// URL details of the request.
        /// </summary>
        public class UrlDetails
        {
            /// <summary>
            /// Full URL.
            /// </summary>
            public string Full { get; set; } = null;
             
            /// <summary>
            /// Raw URL without query.
            /// </summary>
            public string WithoutQuery
            {
                get
                {
                    if (!String.IsNullOrEmpty(Full))
                    {
                        if (Full.Contains("?")) return Full.Substring(0, Full.IndexOf("?"));
                        else return Full;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            /// <summary>
            /// Raw URL elements.
            /// </summary>
            public string[] Elements
            {
                get
                {
                    string rawUrl = WithoutQuery;

                    if (!String.IsNullOrEmpty(rawUrl))
                    {
                        while (rawUrl.Contains("//")) rawUrl = rawUrl.Replace("//", "/");
                        while (rawUrl.StartsWith("/")) rawUrl = rawUrl.Substring(1);
                        while (rawUrl.EndsWith("/")) rawUrl = rawUrl.Substring(0, rawUrl.Length - 1);
                        string[] encoded = rawUrl.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (encoded != null && encoded.Length > 0)
                        {
                            string[] decoded = new string[encoded.Length];
                            for (int i = 0; i < encoded.Length; i++)
                            {
                                decoded[i] = WebUtility.UrlDecode(encoded[i]);
                            }

                            return decoded;
                        }
                    }

                    return null;
                }
            }

            /// <summary>
            /// Parameters found within the URL, if using parameter routes.
            /// </summary>
            public NameValueCollection Parameters
            {
                get
                {
                    return _Parameters;
                }
                set
                {
                    if (value == null) _Parameters = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                    else _Parameters = value;
                }
            }

            /// <summary>
            /// URL details.
            /// </summary>
            public UrlDetails()
            {

            }

            /// <summary>
            /// URL details.
            /// </summary>
            /// <param name="fullUrl">Full URL.</param> 
            public UrlDetails(string fullUrl)
            {
                if (String.IsNullOrEmpty(fullUrl)) throw new ArgumentNullException(nameof(fullUrl));  
                Full = fullUrl; 
            }

            private NameValueCollection _Parameters = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Query details.
        /// </summary>
        public class QueryDetails
        {
            /// <summary>
            /// Querystring, excluding the leading '?'.
            /// </summary>
            public string Querystring
            {
                get
                {
                    if (_FullUrl.Contains("?"))
                    {
                        return _FullUrl.Substring(_FullUrl.IndexOf("?") + 1, (_FullUrl.Length - _FullUrl.IndexOf("?") - 1));
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            /// <summary>
            /// Query elements.
            /// </summary>
            public NameValueCollection Elements
            {
                get
                {
                    NameValueCollection ret = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                    string qs = Querystring;
                    if (!String.IsNullOrEmpty(qs))
                    {
                        string[] queries = qs.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                        if (queries.Length > 0)
                        {
                            for (int i = 0; i < queries.Length; i++)
                            {
                                string[] queryParts = queries[i].Split('=');
                                if (queryParts != null && queryParts.Length == 2)
                                {
                                    ret.Add(queryParts[0], queryParts[1]);
                                }
                                else if (queryParts != null && queryParts.Length == 1)
                                {
                                    ret.Add(queryParts[0], null);
                                }
                            }
                        }
                    }

                    return ret;
                }
            }

            /// <summary>
            /// Query details.
            /// </summary>
            public QueryDetails()
            {

            }

            /// <summary>
            /// Query details.
            /// </summary>
            /// <param name="fullUrl">Full URL.</param>
            public QueryDetails(string fullUrl)
            {
                if (String.IsNullOrEmpty(fullUrl)) throw new ArgumentNullException(nameof(fullUrl));

                _FullUrl = fullUrl;
            }

            private string _FullUrl = null;
        }

        #endregion
    }
}
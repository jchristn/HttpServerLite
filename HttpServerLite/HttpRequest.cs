using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CavemanTcp;
using Newtonsoft.Json;

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
        [JsonProperty(Order = -8)]
        public DateTime TimestampUtc { get; private set; } = DateTime.Now.ToUniversalTime();

        /// <summary>
        /// Thread ID on which the request exists.
        /// </summary>
        [JsonProperty(Order = -7)]
        public int ThreadId { get; private set; } = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// The protocol and version.
        /// </summary>
        [JsonProperty(Order = -6)]
        public string ProtocolVersion { get; private set; } = null;

        /// <summary>
        /// Source (requestor) IP and port information.
        /// </summary>
        [JsonProperty(Order = -5)]
        public SourceDetails Source { get; private set; } = new SourceDetails();

        /// <summary>
        /// The HTTP method used in the request.
        /// </summary>
        [JsonProperty(Order = -4)]
        public HttpMethod Method { get; private set; } = HttpMethod.GET;

        /// <summary>
        /// URL details.
        /// </summary>
        [JsonProperty(Order = -3)]
        public UrlDetails Url { get; private set; } = new UrlDetails();

        /// <summary>
        /// Query details.
        /// </summary>
        [JsonProperty(Order = -2)]
        public QueryDetails Query { get; private set; } = new QueryDetails();

        /// <summary>
        /// The headers found in the request.
        /// </summary>
        [JsonProperty(Order = -1)]
        public Dictionary<string, string> Headers { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Specifies whether or not the client requested HTTP keepalives.
        /// </summary>
        public bool Keepalive { get; private set; } = false;

        /// <summary>
        /// Indicates whether or not chunked transfer encoding was detected.
        /// </summary>
        public bool ChunkedTransfer { get; private set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been gzip compressed.
        /// </summary>
        public bool Gzip { get; private set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been deflate compressed.
        /// </summary>
        public bool Deflate { get; private set; } = false;

        /// <summary>
        /// The useragent specified in the request.
        /// </summary>
        public string Useragent { get; private set; } = null;

        /// <summary>
        /// The content type as specified by the requestor (client).
        /// </summary>
        [JsonProperty(Order = 990)]
        public string ContentType { get; private set; } = null;

        /// <summary>
        /// The number of bytes in the request body.
        /// </summary>
        [JsonProperty(Order = 991)]
        public long ContentLength { get; private set; } = 0;

        /// <summary>
        /// Bytes from the DataStream property.  Using Data will fully read the DataStream property and thus it cannot be read again.
        /// </summary>
        public byte[] Data
        {
            get
            {
                if (_Data == null)
                {
                    if (_DataStream != null && _DataStream.CanRead && ContentLength > 0)
                    {
                        _Data = Common.ReadStream(_StreamBufferSize, ContentLength, _DataStream);
                        return _Data;
                    }
                    else
                    {
                        return _Data;
                    }
                }
                else
                {
                    return _Data;
                }
            }
        }

        /// <summary>
        /// The stream containing request data.
        /// </summary>
        [JsonIgnore]
        public Stream DataStream
        {
            get
            {
                return _DataStream;
            }
        }

        #endregion

        #region Private-Members

        private int _StreamBufferSize = 65536;
        private string _IpPort;
        private Stream _Stream = null;
        private string _RequestHeader = null;  
        private byte[] _Data = null;
        private Stream _DataStream = null;

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
            _Stream = stream;

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
        /// Retrieve a specified header value from either the headers or the querystring (case insensitive).
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string RetrieveHeaderValue(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (Headers != null && Headers.Count > 0)
            {
                foreach (KeyValuePair<string, string> curr in Headers)
                {
                    if (String.IsNullOrEmpty(curr.Key)) continue;
                    if (String.Compare(curr.Key.ToLower(), key.ToLower()) == 0) return curr.Value;
                }
            }

            if (Query.Elements != null && Query.Elements.Count > 0)
            {
                foreach (KeyValuePair<string, string> curr in Query.Elements)
                {
                    if (String.IsNullOrEmpty(curr.Key)) continue;
                    if (String.Compare(curr.Key.ToLower(), key.ToLower()) == 0) return curr.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Determine if a header exists.
        /// </summary>
        /// <param name="key">Header key.</param>
        /// <param name="caseSensitive">Specify whether a case sensitive search should be used.</param>
        /// <returns>True if exists.</returns>
        public bool HeaderExists(string key, bool caseSensitive)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (caseSensitive)
            {
                return Headers.ContainsKey(key);
            }
            else
            {
                if (Headers != null && Headers.Count > 0)
                {
                    foreach (KeyValuePair<string, string> header in Headers)
                    {
                        if (String.IsNullOrEmpty(header.Key)) continue;
                        if (header.Key.ToLower().Trim().Equals(key)) return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Determine if a querystring entry exists.
        /// </summary>
        /// <param name="key">Querystring key.</param>
        /// <param name="caseSensitive">Specify whether a case sensitive search should be used.</param>
        /// <returns>True if exists.</returns>
        public bool QuerystringExists(string key, bool caseSensitive)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (caseSensitive)
            {
                return Query.Elements.ContainsKey(key);
            }
            else
            {
                if (Query.Elements != null && Query.Elements.Count > 0)
                {
                    foreach (KeyValuePair<string, string> queryElement in Query.Elements)
                    {
                        if (String.IsNullOrEmpty(queryElement.Key)) continue;
                        if (queryElement.Key.ToLower().Trim().Equals(key)) return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Read the data stream fully and retrieve the string data contained within.
        /// Note: if you use this method, you will not be able to read from the data stream afterward.
        /// </summary>
        /// <returns>String.</returns>
        public string DataAsString()
        {
            if (Data == null) return null;
            return Encoding.UTF8.GetString(Data);
        }

        /// <summary>
        /// Read the data stream fully and convert the data to the object type specified using JSON deserialization.
        /// Note: if you use this method, you will not be able to read from the data stream afterward.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <returns>Object of type specified.</returns>
        public T DataAsJsonObject<T>()where T : class
        {
            string json = DataAsString();
            if (String.IsNullOrEmpty(json)) return null;
            return SerializationHelper.DeserializeJson<T>(json);
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

                    Method = (HttpMethod)Enum.Parse(typeof(HttpMethod), requestLine[0], true); 
                    Url = new UrlDetails(requestLine[1]);
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

                        Headers = Common.AddToDict(key, val, Headers);
                    }

                    #endregion
                }
            }

            #endregion

            #region Payload

            _DataStream = _Stream;

            #endregion
        }
         
        private static Dictionary<string, string> AddToDict(string key, string val, Dictionary<string, string> existing)
        {
            if (String.IsNullOrEmpty(key)) return existing;

            Dictionary<string, string> ret = new Dictionary<string, string>();

            if (existing == null)
            {
                ret.Add(key, val);
                return ret;
            }
            else
            {
                if (existing.ContainsKey(key))
                {
                    if (String.IsNullOrEmpty(val)) return existing;
                    string tempVal = existing[key];
                    tempVal += "," + val;
                    existing.Remove(key);
                    existing.Add(key, tempVal);
                    return existing;
                }
                else
                {
                    existing.Add(key, val);
                    return existing;
                }
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
            public string IpAddress { get; private set; } = null;

            /// <summary>
            /// TCP port from which the request originated on the requestor.
            /// </summary>
            public int Port { get; private set; } = 0;

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
            public string Full { get; private set; } = null;
             
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
            public Dictionary<string, string> Parameters { get; internal set; } = new Dictionary<string, string>();

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
            public Dictionary<string, string> Elements
            {
                get
                {
                    Dictionary<string, string> ret = new Dictionary<string, string>();
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
                                    ret = AddToDict(queryParts[0], queryParts[1], ret);
                                }
                                else if (queryParts != null && queryParts.Length == 1)
                                {
                                    ret = AddToDict(queryParts[0], null, ret);
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
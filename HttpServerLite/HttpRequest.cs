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
        public DateTime TimestampUtc = DateTime.Now.ToUniversalTime();

        /// <summary>
        /// Thread ID on which the request exists.
        /// </summary>
        [JsonProperty(Order = -7)]
        public int ThreadId { get; private set; } = 0;

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
        /// URL components.
        /// </summary>
        [JsonProperty(Order = -3)]
        public UrlComponents Url { get; private set; } = new UrlComponents();

        /// <summary>
        /// Query components.
        /// </summary>
        [JsonProperty(Order = -2)]
        public QueryComponents Query { get; private set; } = new QueryComponents();

        /// <summary>
        /// The headers found in the request.
        /// </summary>
        [JsonProperty(Order = -1)]
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
        /// Specifies whether or not the client requested HTTP keepalives.
        /// </summary>
        public bool Keepalive { get; private set; } = false;

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
        public int ContentLength { get; private set; } = 0;

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
        private Dictionary<string, string> _Headers = new Dictionary<string, string>();
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
            if (_Headers != null && _Headers.Count > 0)
            {
                foreach (KeyValuePair<string, string> curr in _Headers)
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
                return _Headers.ContainsKey(key);
            }
            else
            {
                if (_Headers != null && _Headers.Count > 0)
                {
                    foreach (KeyValuePair<string, string> header in _Headers)
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
                    Url = new UrlComponents(requestLine[1]);
                    Query = new QueryComponents(ExtractQuery(Url.WithQuery));
                     
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
                        else
                        {
                            _Headers = Common.AddToDict(key, val, _Headers);
                        }
                    }

                    #endregion
                }
            }

            #endregion

            #region Payload

            _DataStream = _Stream;

            #endregion
        }

        private string ExtractQuery(string full)
        {
            if (String.IsNullOrEmpty(full)) return null;
            if (!full.Contains("?")) return null;

            int qsStartPos = full.IndexOf("?");
            if (qsStartPos >= (full.Length - 1)) return null;
            return full.Substring(qsStartPos + 1);
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
        /// URL components of the request.
        /// </summary>
        public class UrlComponents
        {
            /// <summary>
            /// URL including host and querystring (i.e. /root/child?foo=bar).
            /// </summary>
            public string WithQuery { get; private set; } = null;

            /// <summary>
            /// URL without querystring (i.e. /root/child).
            /// </summary>
            public string WithoutQuery { get; private set; } = null;

            /// <summary>
            /// URL entries (i.e. /root/child becomes [0]: root and [1]: child).
            /// </summary>
            public string[] Entries { get; private set; } = null;

            /// <summary>
            /// Instantiate the object.
            /// </summary>
            public UrlComponents()
            {

            }

            /// <summary>
            /// Instantiate the object.
            /// </summary>
            /// <param name="url">URL with query.</param>
            public UrlComponents(string url)
            {
                WithQuery = url; 
                WithoutQuery = UrlWithoutQuery(WithQuery);
                Entries = ExtractElements(WithoutQuery);
            }

            private string UrlWithoutQuery(string full)
            {
                if (String.IsNullOrEmpty(full)) return null;
                if (!full.Contains("?")) return full;
                return full.Substring(0, full.IndexOf("?"));
            }

            private string[] ExtractElements(string withoutQuery)
            {
                if (String.IsNullOrEmpty(withoutQuery)) return null;

                int position = 0;
                string tempString = "";
                List<string> ret = new List<string>();

                foreach (char c in withoutQuery)
                {
                    if ((position == 0) &&
                        (String.Compare(tempString, "") == 0) &&
                        (c == '/'))
                    {
                        // skip the first slash
                        continue;
                    }

                    if ((c != '/') && (c != '?'))
                    {
                        tempString += c;
                    }

                    if ((c == '/') || (c == '?'))
                    {
                        if (!String.IsNullOrEmpty(tempString))
                        {
                            // add to raw URL entries list
                            ret.Add(tempString);
                        }

                        position++;
                        tempString = "";
                    }
                }

                if (!String.IsNullOrEmpty(tempString))
                {
                    // add to raw URL entries list
                    ret.Add(tempString);
                }

                return ret.ToArray();
            } 
        }

        /// <summary>
        /// Query components of the request.
        /// </summary>
        public class QueryComponents
        {
            /// <summary>
            /// Full querystring.
            /// </summary>
            public string Full { get; private set; } = null;

            /// <summary>
            /// Querystring entries.
            /// </summary>
            public Dictionary<string, string> Elements { get; private set; } = new Dictionary<string, string>();

            /// <summary>
            /// Instantiate the object.
            /// </summary>
            public QueryComponents()
            {

            }

            /// <summary>
            /// Instantiate the object.
            /// </summary>
            /// <param name="query"></param>
            public QueryComponents(string query)
            {
                Full = query;
                Elements = ExtractEntries(query);
            }
             
            private Dictionary<string, string> ExtractEntries(string query)
            {
                if (String.IsNullOrEmpty(query)) return new Dictionary<string, string>();

                Dictionary<string, string> ret = new Dictionary<string, string>();

                string[] entries = query.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

                if (entries != null && entries.Length > 0)
                {
                    foreach (string entry in entries)
                    {
                        string[] entryParts = entry.Split(new[] { '=' }, 2);

                        if (entryParts != null && entryParts.Length > 0)
                        {
                            string key = entryParts[0];
                            string val = null;

                            if (entryParts.Length == 2)
                            {
                                val = entryParts[1];
                            }

                            ret = Common.AddToDict(key, val, ret);
                        }
                    }
                }

                return ret;
            }
        }

        #endregion
    }
}
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
        public DateTime TimestampUtc;

        /// <summary>
        /// Thread ID on which the request exists.
        /// </summary>
        public int ThreadId;

        /// <summary>
        /// The protocol and version.
        /// </summary>
        public string ProtocolVersion;
         
        /// <summary>
        /// IP address of the requestor (client).
        /// </summary>
        public string SourceIp;

        /// <summary>
        /// TCP port from which the request originated on the requestor (client).
        /// </summary>
        public int SourcePort;

        /// <summary>
        /// IP address of the recipient (server).
        /// </summary>
        public string DestIp;

        /// <summary>
        /// TCP port on which the request was received by the recipient (server).
        /// </summary>
        public int DestPort;

        /// <summary>
        /// The destination hostname as found in the request line, if present.
        /// </summary>
        public string DestHostname;

        /// <summary>
        /// The destination host port as found in the request line, if present.
        /// </summary>
        public int DestHostPort;

        /// <summary>
        /// Specifies whether or not the client requested HTTP keepalives.
        /// </summary>
        public bool Keepalive;

        /// <summary>
        /// The HTTP method used in the request.
        /// </summary>
        public HttpMethod Method;
         
        /// <summary>
        /// The full URL as sent by the requestor (client).
        /// </summary>
        public string FullUrl;

        /// <summary>
        /// The raw (relative) URL with the querystring attached.
        /// </summary>
        public string RawUrlWithQuery;

        /// <summary>
        /// The raw (relative) URL without the querystring attached.
        /// </summary>
        public string RawUrlWithoutQuery;

        /// <summary>
        /// List of items found in the raw URL.
        /// </summary>
        public List<string> RawUrlEntries;

        /// <summary>
        /// The querystring attached to the URL.
        /// </summary>
        public string Querystring;

        /// <summary>
        /// Dictionary containing key-value pairs from items found in the querystring.
        /// </summary>
        public Dictionary<string, string> QuerystringEntries;

        /// <summary>
        /// The useragent specified in the request.
        /// </summary>
        public string Useragent;

        /// <summary>
        /// The number of bytes in the request body.
        /// </summary>
        public int ContentLength;

        /// <summary>
        /// The content type as specified by the requestor (client).
        /// </summary>
        public string ContentType;

        /// <summary>
        /// The headers found in the request.
        /// </summary>
        public Dictionary<string, string> Headers;

        /// <summary>
        /// The stream from which to read the request body sent by the requestor (client).
        /// </summary>
        public byte[] Data = null;

        #endregion

        #region Private-Members

        private string _IpPort;
        private TcpServer _Tcp;
        private byte[] _HeaderBytes = null;
        private Uri _Uri; 

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpRequest()
        {
            ThreadId = Thread.CurrentThread.ManagedThreadId;
            TimestampUtc = DateTime.Now.ToUniversalTime();
            QuerystringEntries = new Dictionary<string, string>();
            Headers = new Dictionary<string, string>();
        }

        /// <summary>
        /// Create an HttpRequest object from a byte array.
        /// </summary>
        /// <param name="ipPort">IP:port of the requestor.</param>
        /// <param name="bytes">Bytes.</param>
        /// <returns>HttpRequest.</returns>
        public HttpRequest(string ipPort, byte[] bytes, TcpServer server)
        {
            _IpPort = ipPort ?? throw new ArgumentNullException(nameof(ipPort));
            _HeaderBytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
            _Tcp = server ?? throw new ArgumentNullException(nameof(server));

            Build();
        }
         
        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve a string-formatted, human-readable copy of the HttpRequest instance.
        /// </summary>
        /// <returns>String-formatted, human-readable copy of the HttpRequest instance.</returns>
        public override string ToString()
        {
            string ret = "";

            ret += "--- HTTP Request ---" + Environment.NewLine;
            ret += TimestampUtc.ToString("MM/dd/yyyy HH:mm:ss") + " " + SourceIp + ":" + SourcePort + " to " + DestIp + ":" + DestPort + Environment.NewLine;
            ret += "  " + Method + " " + RawUrlWithoutQuery + " " + ProtocolVersion + Environment.NewLine;
            ret += "  Full URL    : " + FullUrl + Environment.NewLine;
            ret += "  Raw URL     : " + RawUrlWithoutQuery + Environment.NewLine;
            ret += "  Querystring : " + Querystring + Environment.NewLine;
            ret += "  Useragent   : " + Useragent + " (Keepalive " + Keepalive + ")" + Environment.NewLine;
            ret += "  Content     : " + ContentType + " (" + ContentLength + " bytes)" + Environment.NewLine;
            ret += "  Destination : " + DestHostname + ":" + DestHostPort + Environment.NewLine;

            if (Headers != null && Headers.Count > 0)
            {
                ret += "  Headers     : " + Environment.NewLine;
                foreach (KeyValuePair<string, string> curr in Headers)
                {
                    ret += "    " + curr.Key + ": " + curr.Value + Environment.NewLine;
                }
            }
            else
            {
                ret += "  Headers     : none" + Environment.NewLine;
            }

            return ret;
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

            if (QuerystringEntries != null && QuerystringEntries.Count > 0)
            {
                foreach (KeyValuePair<string, string> curr in QuerystringEntries)
                {
                    if (String.IsNullOrEmpty(curr.Key)) continue;
                    if (String.Compare(curr.Key.ToLower(), key.ToLower()) == 0) return curr.Value;
                }
            }

            return null;
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
        public T DataAsJsonObject<T>() where T : class
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
            SourceIp = "unknown";
            SourcePort = 0;
            Common.ParseIpPort(_IpPort, out SourceIp, out SourcePort);

            ThreadId = Thread.CurrentThread.ManagedThreadId;
            DestIp = "unknown";
            DestPort = 0;
            Headers = new Dictionary<string, string>();

            #endregion

            #region Convert-to-String-List

            string str = Encoding.UTF8.GetString(_HeaderBytes);
            string[] headers = str.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

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
                    FullUrl = requestLine[1];
                    ProtocolVersion = requestLine[2];
                    RawUrlWithQuery = FullUrl;
                    RawUrlWithoutQuery = ExtractRawUrlWithoutQuery(RawUrlWithQuery);
                    RawUrlEntries = ExtractRawUrlEntries(RawUrlWithoutQuery);
                    Querystring = ExtractQuerystring(RawUrlWithQuery);
                    QuerystringEntries = ExtractQuerystringEntries(Querystring);

                    try
                    {
                        _Uri = new Uri(FullUrl);
                        DestHostname = _Uri.Host;
                        DestHostPort = _Uri.Port;
                    }
                    catch (Exception)
                    {
                    }

                    if (String.IsNullOrEmpty(DestHostname))
                    {
                        if (!FullUrl.Contains("://") & FullUrl.Contains(":"))
                        {
                            string[] hostAndPort = FullUrl.Split(':');
                            if (hostAndPort.Length == 2)
                            {
                                DestHostname = hostAndPort[0];
                                if (!Int32.TryParse(hostAndPort[1], out DestHostPort))
                                {
                                    throw new Exception("Unable to parse destination hostname and port.");
                                }
                            }
                        }
                    }

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
                            Headers = AddToDict(key, val, Headers);
                        }
                    }

                    #endregion
                }
            }

            #endregion

            #region Payload

            if (ContentLength > 0)
                Data = _Tcp.ReadBytes(_IpPort, ContentLength);

            #endregion
        }

        private static string ExtractRawUrlWithoutQuery(string rawUrlWithQuery)
        {
            if (String.IsNullOrEmpty(rawUrlWithQuery)) return null;
            if (!rawUrlWithQuery.Contains("?")) return rawUrlWithQuery;
            return rawUrlWithQuery.Substring(0, rawUrlWithQuery.IndexOf("?"));
        }

        private static List<string> ExtractRawUrlEntries(string rawUrlWithoutQuery)
        {
            if (String.IsNullOrEmpty(rawUrlWithoutQuery)) return null;

            int position = 0;
            string tempString = "";
            List<string> ret = new List<string>();

            foreach (char c in rawUrlWithoutQuery)
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

            return ret;
        }

        private static string ExtractQuerystring(string rawUrlWithQuery)
        {
            if (String.IsNullOrEmpty(rawUrlWithQuery)) return null;
            if (!rawUrlWithQuery.Contains("?")) return null;

            int qsStartPos = rawUrlWithQuery.IndexOf("?");
            if (qsStartPos >= (rawUrlWithQuery.Length - 1)) return null;
            return rawUrlWithQuery.Substring(qsStartPos + 1);
        }

        private static Dictionary<string, string> ExtractQuerystringEntries(string query)
        {
            if (String.IsNullOrEmpty(query)) return null;

            Dictionary<string, string> ret = new Dictionary<string, string>();

            int inKey = 1;
            int inVal = 0;
            int position = 0;
            string tempKey = "";
            string tempVal = "";

            foreach (char c in query)
            {
                if (inKey == 1)
                {
                    if (c != '=')
                    {
                        tempKey += c;
                    }
                    else
                    {
                        inKey = 0;
                        inVal = 1;
                        continue;
                    }
                }

                if (inVal == 1)
                {
                    if (c != '&')
                    {
                        tempVal += c;
                    }
                    else
                    {
                        inKey = 1;
                        inVal = 0;

                        if (!String.IsNullOrEmpty(tempVal)) tempVal = WebUtility.UrlEncode(tempVal);
                        ret = AddToDict(tempKey, tempVal, ret);

                        tempKey = "";
                        tempVal = "";
                        position++;
                        continue;
                    }
                }

                if (inVal == 1)
                {
                    if (!String.IsNullOrEmpty(tempVal)) tempVal = WebUtility.UrlEncode(tempVal);
                    ret = AddToDict(tempKey, tempVal, ret);
                }
            }

            return ret;
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
    }
}
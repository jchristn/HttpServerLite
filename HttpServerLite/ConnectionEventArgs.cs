using System;
using System.Collections.Generic;
using System.Text;

namespace HttpServerLite
{
    /// <summary>
    /// Connection received event arguments.
    /// </summary>
    public class ConnectionEventArgs : EventArgs
    {
        /// <summary>
        /// IP address.
        /// </summary>
        public string Ip { get; private set; }

        /// <summary>
        /// Port number.
        /// </summary>
        public int Port { get; private set; }

        internal ConnectionEventArgs(string ip, int port)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));

            Ip = ip;
            Port = port;
        }
    }
}

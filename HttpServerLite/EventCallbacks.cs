using System;
using System.Collections.Generic;
using System.Text;

namespace HttpServerLite
{
    /// <summary>
    /// Callbacks/actions to use when various events are encountered.
    /// </summary>
    public class EventCallbacks
    {
        #region Public-Members

        /// <summary>
        /// Callback/action to call when a connection is received.
        /// string: IP address of the client.
        /// int: Source TCP port of the client.
        /// </summary>
        public Action<string, int> ConnectionReceived
        {
            get
            {
                return _ConnectionReceived;
            }
            set
            {
                if (value == null) _ConnectionReceived = ConnectionReceivedInternal;
                else _ConnectionReceived = value;
            }
        }

        /// <summary>
        /// Callback/action to call when a request is received.
        /// string: IP address of the client.
        /// int: Source TCP port of the client.
        /// string: HTTP method.
        /// string: Full URL.
        /// </summary>
        public Action<string, int, string, string> RequestReceived
        {
            get
            {
                return _RequestReceived;
            }
            set
            {
                if (value == null) _RequestReceived = RequestReceivedInternal;
                else _RequestReceived = value;
            }
        }

        /// <summary>
        /// Callback/action to call when a request is denied due to access control.
        /// string: IP address of the client.
        /// int: Source TCP port of the client.
        /// string: HTTP method.
        /// string: Full URL.
        /// </summary>
        public Action<string, int, string, string> AccessControlDenied
        {
            get
            {
                return _AccessControlDenied;
            }
            set
            {
                if (value == null) _AccessControlDenied = AccessControlDeniedInternal;
                else _AccessControlDenied = value;
            }
        }
         
        /// <summary>
        /// Callback/action to call when a response is sent.
        /// string: IP address of the client.
        /// int: Source TCP port of the client.
        /// string: HTTP method.
        /// string: Full URL.
        /// int: Response status code.
        /// double: Number of milliseconds.
        /// </summary>
        public Action<string, int, string, string, int, double> ResponseSent
        {
            get
            {
                return _ResponseSent;
            }
            set
            {
                if (value == null) _ResponseSent = ResponseSentInternal;
                else _ResponseSent = value; 
            }
        }

        /// <summary>
        /// Callback/action to call when an exception is encountered.
        /// string: IP address of the client.
        /// int: Source TCP port of the client.
        /// Exception: Exception encountered.
        /// </summary>
        public Action<string, int, Exception> ExceptionEncountered
        {
            get
            {
                return _ExceptionEncountered;
            }
            set
            {
                if (value == null) _ExceptionEncountered = ExceptionEncounteredInternal;
                else _ExceptionEncountered = value;
            }
        }

        /// <summary>
        /// Callback/action to call when the server is started.
        /// </summary>
        public Action ServerStarted
        {
            get
            {
                return _ServerStarted;
            }
            set
            {
                if (value == null) _ServerStarted = ServerStartedInternal;
                else _ServerStarted = value;
            }
        }

        /// <summary>
        /// Callback/action to call when the server is stopped.
        /// </summary>
        public Action ServerStopped
        {
            get
            {
                return _ServerStopped;
            }
            set
            {
                if (value == null) _ServerDisposed = ServerStoppedInternal;
                else _ServerStopped = value;
            }
        }

        /// <summary>
        /// Callback/action to call when the server is disposed.
        /// </summary>
        public Action ServerDisposed
        {
            get
            {
                return _ServerDisposed;
            }
            set
            {
                if (value == null) _ServerDisposed = ServerDisposedInternal;
                else _ServerDisposed = value;
            }
        }

        #endregion

        #region Private-Members

        private Action<string, int> _ConnectionReceived = null;
        private Action<string, int, string, string> _RequestReceived = null;
        private Action<string, int, string, string> _AccessControlDenied = null; 
        private Action<string, int, string, string, int, double> _ResponseSent = null;
        private Action<string, int, Exception> _ExceptionEncountered = null;
        private Action _ServerStarted = null;
        private Action _ServerStopped = null;
        private Action _ServerDisposed = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public EventCallbacks()
        {
            _ConnectionReceived = ConnectionReceivedInternal;
            _RequestReceived = RequestReceivedInternal;
            _AccessControlDenied = AccessControlDeniedInternal;
            _ResponseSent = ResponseSentInternal;
            _ExceptionEncountered = ExceptionEncounteredInternal;
            _ServerStarted = ServerStartedInternal;
            _ServerDisposed = ServerDisposedInternal;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private void ConnectionReceivedInternal(string ip, int port)
        {
        }

        private void RequestReceivedInternal(string ip, int port, string method, string url)
        {
        }

        private void AccessControlDeniedInternal(string ip, int port, string method, string url)
        {
        }

        private void ResponseSentInternal(string ip, int port, string method, string url, int status, double totalTimeMs)
        {
        }

        private void ExceptionEncounteredInternal(string ip, int port, Exception e)
        {
        }

        private void ServerStartedInternal()
        {
        }

        private void ServerStoppedInternal()
        {
        }

        private void ServerDisposedInternal()
        {
        }

        #endregion
    }
}
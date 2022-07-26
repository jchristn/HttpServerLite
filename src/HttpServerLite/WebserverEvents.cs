using System;
using System.Collections.Generic;
using System.Text;

namespace HttpServerLite
{
    /// <summary>
    /// Events to fire when various events are encountered.
    /// </summary>
    public class WebserverEvents
    {
        #region Public-Members

        /// <summary>
        /// Method to use for sending log messages.
        /// </summary>
        public Action<string> Logger = null;

        /// <summary>
        /// Event to fire when a connection is received.
        /// string: IP address of the client.
        /// int: Source TCP port of the client.
        /// </summary>
        public event EventHandler ConnectionReceived = delegate { }; 

        /// <summary>
        /// Event to fire when a connection is denied.
        /// string: IP address of the client
        /// int: Source TCP port of the client
        /// </summary>
        public event EventHandler ConnectionDenied = delegate { };

        /// <summary>
        /// Event to fire when a request is received. 
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestReceived = delegate { };

        /// <summary>
        /// Event to fire when a request is denied due to access control. 
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestDenied = delegate { };

        /// <summary>
        /// Event to fire when a response is sent. 
        /// </summary>
        public event EventHandler<ResponseEventArgs> ResponseSent = delegate { };

        /// <summary>
        /// Event to fire when an exception is encountered. 
        /// </summary>
        public event EventHandler<ExceptionEventArgs> Exception = delegate { };

        /// <summary>
        /// Event to fire when the server is started.
        /// </summary>
        public event EventHandler ServerStarted = delegate { };

        /// <summary>
        /// Event to fire when the server is stopped.
        /// </summary>
        public event EventHandler ServerStopped = delegate { };

        /// <summary>
        /// Event to fire when the server is being disposed.
        /// </summary>
        public event EventHandler ServerDisposing = delegate { };

        #endregion

        #region Private-Members
         
        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public WebserverEvents()
        { 

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Internal-Methods

        internal void HandleConnectionReceived(object sender, ConnectionEventArgs args)
        {
            WrappedEventHandler(() => ConnectionReceived?.Invoke(sender, args), "ConnectionReceived", sender);
        }

        internal void HandleConnectionDenied(object sender, ConnectionEventArgs args)
        {
            WrappedEventHandler(() => ConnectionDenied?.Invoke(sender, args), "ConnectionDenied", sender);
        }

        internal void HandleRequestReceived(object sender, RequestEventArgs args)
        {
            WrappedEventHandler(() => RequestReceived?.Invoke(sender, args), "RequestReceived", sender);
        }

        internal void HandleRequestDenied(object sender, RequestEventArgs args)
        {
            WrappedEventHandler(() => RequestDenied?.Invoke(sender, args), "RequestDenied", sender);
        }

        internal void HandleResponseSent(object sender, ResponseEventArgs args)
        {
            WrappedEventHandler(() => ResponseSent?.Invoke(sender, args), "ResponseSent", sender);
        }

        internal void HandleException(object sender, ExceptionEventArgs args)
        {
            WrappedEventHandler(() => Exception?.Invoke(sender, args), "Exception", sender);
        }

        internal void HandleServerStarted(object sender, EventArgs args)
        {
            WrappedEventHandler(() => ServerStarted?.Invoke(sender, args), "ServerStarted", sender);
        }

        internal void HandleServerStopped(object sender, EventArgs args)
        {
            WrappedEventHandler(() => ServerStopped?.Invoke(sender, args), "ServerStopped", sender);
        }

        internal void HandleServerDisposing(object sender, EventArgs args)
        {
            WrappedEventHandler(() => ServerDisposing?.Invoke(sender, args), "ServerDisposing", sender);
        }

        #endregion

        #region Private-Methods

        private void WrappedEventHandler(Action action, string handler, object sender)
        {
            if (action == null) return;
             
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                Logger?.Invoke("Event handler exception in " + handler + ": " + Environment.NewLine + SerializationHelper.SerializeJson(e, true));
            }
        }
         
        #endregion
    }
}
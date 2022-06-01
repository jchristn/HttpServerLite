using System;
using System.Collections.Generic;
using System.Text;

namespace HttpServerLite
{
    /// <summary>
    /// Callbacks to invoke under various conditions.
    /// </summary>
    public class WebserverCallbacks
    {
        #region Public-Members

        /// <summary>
        /// Method to invoke when a connection is received to authorize further processing.
        /// </summary>
        public Func<string, int, bool> AuthorizeConnection = null;

        #endregion

        #region Private-Members
         
        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public WebserverCallbacks()
        { 

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Internal-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
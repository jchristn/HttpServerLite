﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HttpServerLite
{
    /// <summary>
    /// Assign a method handler for when requests are received matching the supplied method and path containing parameters.
    /// </summary>
    public class ParameterRoute
    {
        #region Public-Members

        /// <summary>
        /// The HTTP method, i.e. GET, PUT, POST, DELETE, etc.
        /// </summary>
        public HttpMethod Method;

        /// <summary>
        /// The pattern against which the raw URL should be matched.  
        /// </summary>
        public string Path;

        /// <summary>
        /// The handler for the parameter route.
        /// </summary>
        public Func<HttpContext, Task> Handler;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a new route object.
        /// </summary>
        /// <param name="method">The HTTP method, i.e. GET, PUT, POST, DELETE, etc.</param>
        /// <param name="path">The pattern against which the raw URL should be matched.</param>
        /// <param name="handler">The method that should be called to handle the request.</param>
        public ParameterRoute(HttpMethod method, string path, Func<HttpContext, Task> handler)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Method = method;
            Path = path;
            Handler = handler;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}

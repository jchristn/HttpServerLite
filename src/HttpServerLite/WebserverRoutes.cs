using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpServerLite
{
    /// <summary>
    /// Webserver routes.
    /// </summary>
    public class WebserverRoutes
    {
        #region Public-Members

        /// <summary>
        /// Function to call when a preflight (OPTIONS) request is received.  
        /// Often used to handle CORS.  
        /// Leave null to use the default OPTIONS handler.
        /// </summary>
        public Func<HttpContext, Task> Preflight
        {
            get
            {
                return _Preflight;
            }
            set
            {
                if (value == null) _Preflight = PreflightInternal;
                else _Preflight = value;
            }
        }

        /// <summary>
        /// Function to call prior to routing.  
        /// Return 'true' if the connection should be terminated.
        /// Return 'false' to allow the connection to continue routing.
        /// </summary>
        public Func<HttpContext, Task<bool>> PreRouting = null;

        /// <summary>
        /// Function to call after routing.  
        /// </summary>
        public Func<HttpContext, Task> PostRouting = null;

        /// <summary>
        /// Content routes; i.e. routes to specific files or folders for GET and HEAD requests.
        /// </summary>
        public ContentRouteManager Content
        {
            get
            {
                return _Content;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Content));
                _Content = value;
            }
        }

        /// <summary>
        /// Handler for content route requests.
        /// </summary>
        public ContentRouteHandler ContentHandler
        {
            get
            {
                return _ContentHandler;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(ContentHandler));
                _ContentHandler = value;
            }
        }

        /// <summary>
        /// Static routes; i.e. routes with explicit matching and any HTTP method.
        /// </summary>
        public StaticRouteManager Static
        {
            get
            {
                return _Static;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Static));
                _Static = value;
            }
        }

        /// <summary>
        /// Parameter routes; i.e. routes with parameters embedded in the URL, such as /{version}/api/{id}.
        /// </summary>
        public ParameterRouteManager Parameter
        {
            get
            {
                return _Parameter;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Parameter));
                _Parameter = value;
            }
        }

        /// <summary>
        /// Dynamic routes; i.e. routes with regex matching and any HTTP method.
        /// </summary>
        public DynamicRouteManager Dynamic
        {
            get
            {
                return _Dynamic;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Dynamic));
                _Dynamic = value;
            }
        }

        /// <summary>
        /// Default route; used when no other routes match.
        /// </summary>
        public Func<HttpContext, Task> Default
        {
            get
            {
                return _Default;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Default));
                _Default = value;
            }
        }

        #endregion

        #region Private-Members

        private ContentRouteManager _Content = new ContentRouteManager();
        private ContentRouteHandler _ContentHandler = null;

        private StaticRouteManager _Static = new StaticRouteManager();
        private ParameterRouteManager _Parameter = new ParameterRouteManager();
        private DynamicRouteManager _Dynamic = new DynamicRouteManager();
        private Func<HttpContext, Task> _Default = null;
        private Func<HttpContext, Task> _Preflight = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object using default settings.
        /// </summary>
        public WebserverRoutes()
        {
            _Preflight = PreflightInternal;
            _ContentHandler = new ContentRouteHandler(_Content); 
        }

        /// <summary>
        /// Instantiate the object using default settings and the specified default route.
        /// </summary>
        public WebserverRoutes(Func<HttpContext, Task> defaultRoute)
        {
            if (defaultRoute == null) throw new ArgumentNullException(nameof(defaultRoute));
            
            _Preflight = PreflightInternal;
            _Default = defaultRoute;
            _ContentHandler = new ContentRouteHandler(_Content);
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private async Task PreflightInternal(HttpContext ctx)
        {
            try
            {
                ctx.Response.StatusCode = 200;

                string[] requestedHeaders = null;
                if (ctx.Request.Headers != null)
                {
                    for (int i = 0; i < ctx.Request.Headers.Count; i++)
                    {
                        string key = ctx.Request.Headers.GetKey(i);
                        string val = ctx.Request.Headers.Get(i);

                        if (String.IsNullOrEmpty(key)) continue;
                        if (String.IsNullOrEmpty(val)) continue;
                        if (String.Compare(key.ToLower(), "access-control-request-headers") == 0)
                        {
                            requestedHeaders = val.Split(',');
                            break;
                        }
                    }
                }

                string headers = "";

                if (requestedHeaders != null)
                {
                    int addedCount = 0;
                    foreach (string curr in requestedHeaders)
                    {
                        if (String.IsNullOrEmpty(curr)) continue;
                        if (addedCount > 0) headers += ", ";
                        headers += ", " + curr;
                        addedCount++;
                    }
                }

                ctx.Response.ContentLength = 0;
                await ctx.Response.SendAsync(0);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        #endregion
    }
}

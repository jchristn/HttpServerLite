# Change Log

## Current Version

v1.2.5

- Add a event WebserverEvents.ConnectionDenied

v1.2.4

- Added constructor with X509Certificate2, thank you @samisil

## Previous Versions

v1.2.3

- Added Callbacks object with callback AuthorizeConnection

v1.2.2

- Added ```Callbacks``` object with callback ```AuthorizeConnection```

v1.2.1

- Parameter routes

v1.2.0

- Breaking changes to synchronize HttpRequest properties with Watson Webserver

v1.1.0

- Breaking changes to improve simplicity and reliability
- Consolidated settings into the ```Settings``` property
- Consolidated routing into the ```Routing``` property
- Use of ```EventHandler``` for events instead of ```Action```
- Use of ```ConfigureAwait``` for reliability within your application
- Simplified constructors
- Pages property to set how 404 and 500 responses should be sent, if not handled within your application
- Attribute-based routes now loaded automatically, removed ```LoadRoutes``` method
- Restructured ```HttpContext```, ```HttpRequest```, and ```HttpResponse``` for better usability

v1.0.4

- Dependency update
- Configurable match preference in dynamic routes manager

v1.0.3

- Added attributes for both static and dynamic routes

v1.0.2

- Added ```Stop()``` API

v1.0.1

- Added attribute-based static routes (thank you @Job79!)

v1.0.0

- Alpha 

# Change Log

## Current Version

v2.1.x

- ```HostBuilder``` feature to quickly build servers, thank you @sapurtcomputer30!
- Bugfix for ```HttpContext.HttpRequest.Data``` not ending, thank you @ChZhongPengCheng33

## Previous Versions

v2.0.x

- Breaking changes to migrate dictionaries to ```NameValueCollection```
- Retarget to include .NET Framework 4.8 and .NET 7.0
- Reintroduce ```HttpRequest``` methods for checking existence of and retrieving query or header values

v1.2.x

- More efficiency in internal send methods, thank you @marcussacana
- Removal of Newtonsoft.Json
- Dependency update
- Case insensitive dictionaries
- Less restrictive handling of reading chunks
- Add a event WebserverEvents.ConnectionDenied
- Added constructor with X509Certificate2, thank you @samisil
- Added Callbacks object with callback AuthorizeConnection
- Added ```Callbacks``` object with callback ```AuthorizeConnection```
- Parameter routes
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

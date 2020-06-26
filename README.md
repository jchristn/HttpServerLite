# HttpServerLite

TCP-based simple HTTP and HTTPS server, written in C#.

## New in v1.0.0

- Alpha

## Special Thanks

I'd like to extend a special thanks to those that have helped make HttpServerLite better.

- @winkmichael

## Test App

Refer to the ```Test``` project for a working example.

## Accessing from Outside Localhost

When you configure HttpServerLite to listen on ```127.0.0.1``` or ```localhost```, it will only respond to requests received from within the local machine.

To configure access from other nodes outside of ```localhost```, use the following:

- Specify the exact DNS hostname upon which HttpServerLite should listen in the Server constructor. The HOST header on incoming HTTP requests MUST match this value (this is an operating system limitation)
- If you want to listen on more than one hostname or IP address, use ```*``` or ```+```. You MUST run HttpServerLite as administrator for this to work (this is an operating system limitation)
- If you want to use a port number less than 1024, you MUST run HttpServerLite as administrator (this is an operating system limitation)
- Open a port on your firewall to permit traffic on the TCP port upon which HttpServerLite is listening
- You may have to add URL ACLs, i.e. URL bindings, within the operating system using the ```netsh``` command:
  - Check for existing bindings using ```netsh http show urlacl```
  - Add a binding using ```netsh http add urlacl url=http://[hostname]:[port]/ user=everyone listen=yes```
  - Where ```hostname``` and ```port``` are the values you are using in the constructor 
- If you're still having problems, please do not hesitate to file an issue here, and I will do my best to help and update the documentation.
 
## Version History

Refer to CHANGELOG.md for version history.

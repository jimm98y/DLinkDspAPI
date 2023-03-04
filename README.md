# DLinkDspAPI
A client to control the D-Link DSP-W215 smart socket.

## Disclaimer
It might work with other D-Link devices, but I have no way to test them so they are not officially supported.

## DLinkSocketClient
To control the smart socket, use the `DLinkSocketClient`. 

Create `DLinkSocketClient`:
```
DLinkSocketClient dlinkClient = new DLinkSocketClient("192.168.1.13", "admin", "12345678");
```

Default credentials are printed on the smart socket. Sign in:
```
await dlinkClient.LoginAsync();
```

Control the socket:
```
await dlinkClient.TurnOnAsync();
```
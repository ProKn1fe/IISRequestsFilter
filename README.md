Simple DDoS protection module for IIS.

### Requipments:
* IIS 7+
* [Windows SDK](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/) (Only .NET Framework 4.8.1 Development Kit required)

acutil.exe is only thing why you need Windows SDK. If you already have it on machine with IIS you can skip it.

### How to install:
* Download .dll from Releases or build from sources.
* To see module in IIS manager it must be registered in [GAC](https://learn.microsoft.com/en-us/dotnet/framework/app-domains/gac) with gacutil with sample script:
```
"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8.1 Tools\gacutil.exe" -u IISRequestsFilter
"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8.1 Tools\gacutil.exe" -u System.Net.IPNetwork
"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8.1 Tools\gacutil.exe" -u INIFileParser

"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8.1 Tools\gacutil.exe" -i IISRequestsFilter.dll
"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8.1 Tools\gacutil.exe" -i System.Net.IPNetwork.dll
"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8.1 Tools\gacutil.exe" -i INIFileParser.dll
```
* Then go to IIS Manager -> Modules -> Add managed module
* Choose `IISRequestsFilter.RequestsCounterFilter` and use name `IISRequestsFilter`.
* Restart IIS

### How configure:

It will work only on sites which root directory have file named `RequestsCounterFilter.ini`.

Sample configuration with comments:
```
[SETTINGS]
; Interval to reset counter in seconds
CounterInterval = 3
; Entire site requests count to block
SiteRequestCount = 100
; Same page requests count to block
UrlRequestCount = 50
; Block timeout in seconds
BlockInterval = 300
; Whitelisted ip addresses, support ipv4/ipv6 with subnets e.g 10.10.0.0/16, separator - ,
WhiteList = 127.0.0.1/32
; Distribute block to all sites if ip was banned on one of them
CrossSiteBlock = false
; Enable ability to watch ban stats
EnableStatusUrl = true
; Ban stats url, this require create empty .html file with same name in site root directory
StatusUrl = ShowMeSuperSecretsStats
```
For example for default site it will be `C:\inetpub\wwwroot\RequestsCounterFilter.ini`

After any configuration change you must restart site.


### How status page works?

* You need to create empty html file witch have save name as StatusUrl variable in config e.g `ShowMeSuperSecretsStats.html`
* Go to: http://yourwebsite.com/ShowMeSuperSecretsStats

### FAQ
__Question:__ But for what the hell i need to create empty html file for status?

__Answer:__ Server returns 404 before it gets to this module.

### Changelog:
* 1.0: Initial release

# PublicSuffix

A simple tread-safe library providing programmatic access to the public suffix list published at https://publicsuffix.org/. 

## Usage
The **PublicSuffixList** class implements **IReadOnlyList\<string\>** interface.

Example: list all rules
```c#
PublicSuffixList list = new();
foreach(var rule in list) 
{
	Console.WriteLine(rule);
}
```

Example: get apex domain
```c#
PublicSuffixList list = new();
var apexDomain = list.GetApexDomain("store.example.co.uk");
// will output 'example.co.uk'
Console.WriteLine(apexDomain);
```

By default the class will download the list from https://publicsuffix.org/list/public_suffix_list.dat and cache it in PublicSuffix/public_suffix_list.dat under the common application folder. Exclusive access to file is used for inter-process synchronization to guarantee only one instance of the class is creating or updating the cached data. By default the file is updated every 24 hours.
Both the download URL and the data refresh time can be configured.

Optionally, an instance of the **ILoggerFactory** can be passed to the constructor to have inner log messages emitted.

Example: Update data every 12 hours
```
var options = new PublicSuffixListOptions() 
{
    UpdateAfter = TimeSpan.FromHours(12),
};

PublicSuffixList list = new(options, _loggerFactory);
```


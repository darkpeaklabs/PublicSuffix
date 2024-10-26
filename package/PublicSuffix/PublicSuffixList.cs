using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkPeakLabs.PublicSuffix
{
    public class PublicSuffixList : IReadOnlyList<string>
    {
        private static readonly object _lock = new();
        private static readonly IdnMapping _idnMapping = new();

        private readonly PublicSuffixListOptions _options;
        private readonly ILogger<PublicSuffixList> _logger;

        private DateTime? _timestamp;
        private List<string> _list;
        private Dictionary<string, HashSet<string>> _lookupTable;

        private List<string> List 
        {
            get
            {
                lock (_lock)
                {
                    InitializeOrUpdateList();
                    return _list;
                }
            }
        }

        public string this[int index] => List[index];

        public int Count => List.Count;

        public PublicSuffixList()
            : this(new PublicSuffixListOptions())
        {
        }

        public PublicSuffixList(PublicSuffixListOptions options)
            : this(options, null)
        {
        }

        public PublicSuffixList(ILoggerFactory loggerFactory)
            : this(new PublicSuffixListOptions(), loggerFactory)
        {
        }

        public PublicSuffixList(PublicSuffixListOptions options, ILoggerFactory loggerFactory)
        {
            _options = options;
            _logger = loggerFactory.CreateLogger<PublicSuffixList>();
        }

        public IEnumerator<string> GetEnumerator()
        {
            return List.GetEnumerator();
        }

        public string GetDomainApex(string domainName)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(domainName, nameof(domainName));

            var originalNames = domainName.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (originalNames.Length < 3)
            {
                return domainName;
            }

            string lookupDomainName = _idnMapping.GetAscii(domainName).ToUpperInvariant();
            var lookupNames = lookupDomainName.Split('.', StringSplitOptions.RemoveEmptyEntries);

            var originalTld = originalNames[^1];
            var lookupTld = lookupNames[^1];

            if (_lookupTable.TryGetValue(lookupTld, out var upperLevelDomains) && upperLevelDomains != null)
            {
                string rootDomain = null;

                foreach (var upperLevelDomain in upperLevelDomains)
                {
                    bool exception = upperLevelDomain[0] == '!';
                    var upperLevelNames =
                        exception ?
                        upperLevelDomain[1..].Split('.') :
                        upperLevelDomain.Split('.');
                    bool match = true;
                    int lookupIndex = lookupNames.Length - 1;

                    for (var i = upperLevelNames.Length - 1; i >= 0 && lookupIndex >= 0; i--)
                    {
                        if ((i == 0 && upperLevelNames[i] == "*") || lookupNames[--lookupIndex] == upperLevelNames[i])
                        {
                            // match
                        }
                        else
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        var potentialRootDomain = string.Join(".", originalNames[(Math.Max(0, originalNames.Length - upperLevelNames.Length - 2))..]);

                        // Rules: https://github.com/publicsuffix/list/wiki/Format#format
                        // An exclamation mark (!) at the start of a rule marks an exception to a previous wildcard rule. An exception rule takes priority over any other matching rule.
                        if (exception)
                        {
                            return rootDomain;
                        }

                        // If a hostname matches more than one rule in the file, the longest matching rule (the one with the most levels) will be used.
                        if (rootDomain == null || rootDomain.Length < potentialRootDomain.Length)
                        {
                            rootDomain = potentialRootDomain;
                        }
                    }
                }

                return rootDomain ?? $"{originalNames[^2]}.{originalTld}";
            }
            else
            {
                return $"{originalNames[^2]}.{originalTld}";
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return List.GetEnumerator();
        }

        private void InitializeOrUpdateList()
        {
            if (_list == null)
            {
                _list = [];
                InitializeList();
            }
            else if (_options.DownloadFile && (DateTime.UtcNow - _timestamp.Value) > _options.UpdateAfter)
            {
                using var fileStream = AcquireLock();
                UpdateList(fileStream);
                PublicSuffixUtils.ReleaseFileLock(fileStream);
            }
        }

        private FileStream AcquireLock()
        {
            PublicSuffixUtils.CreateDirectoryIfNotExists(Path.GetDirectoryName(_options.FilePath));
            return PublicSuffixUtils.AcquireFileLock(_options.FilePath);
        }

        /// <summary>
        /// Initialize the public suffix list when a new class instance is created
        /// </summary>
        private void InitializeList()
        {
            using var fileStream = AcquireLock();

            if (fileStream.Length > 0)
            {
                _timestamp = File.GetLastWriteTimeUtc(_options.FilePath);
            }

            if (!_timestamp.HasValue || (DateTime.UtcNow - _timestamp.Value) > _options.UpdateAfter)
            {
                if (_options.DownloadFile)
                {
                    UpdateList(fileStream);
                }
                else
                {
                    using var resourceStream = GetType().Assembly.GetManifestResourceStream($"{GetType().Namespace}.data.public_suffix_list.dat");
                    using StreamReader reader = new(stream: resourceStream, encoding: Encoding.UTF8, leaveOpen: false);
                    ReadList(reader, null);
                }
            }
            else
            {
                using StreamReader reader = new(stream: fileStream, encoding: Encoding.UTF8, leaveOpen: true);
                ReadList(reader, null);
            }

            PublicSuffixUtils.ReleaseFileLock(fileStream);
        }

        private void ReadList(StreamReader reader, Action<string> lineAction)
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                AppendSuffix(line);
                lineAction?.Invoke(line);
            }
            PopulateLookupTable();
        }

        private void UpdateList(FileStream fileStream)
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.UserAgent.Clear();
            var type = GetType();
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"{type.FullName}/{type.Assembly.GetName().Version}");

            if (_options.DownloadTimeout.HasValue)
            {
                client.Timeout = _options.DownloadTimeout.Value;
            }

            fileStream.Position = 0;
            using StreamWriter writer = new(
                stream: fileStream,
                encoding: Encoding.UTF8,
                leaveOpen: true);

            using StreamReader reader = new(GetStream(client), encoding: Encoding.UTF8, leaveOpen: false);

            _list.Clear();
            ReadList(reader, (line) => writer.WriteLine(line));
            _timestamp = DateTime.UtcNow;
        }

        private Stream GetStream(HttpClient client)
        {
            int attempt = 0;
            do
            {
                attempt++;
                _logger?.LogDownloadingDataFile(_options.DownloadUrl, attempt);
                if (TryGetStream(client, out var stream, out var error))
                {
                    return stream;
                }

                if (attempt >= 3)
                {
                    throw new PublicSuffixListDownloadException(error.Message, error);
                }

                Thread.Sleep(TimeSpan.FromSeconds(attempt));
            }
            while (true);
        }

        private bool TryGetStream(HttpClient client, out Stream stream, out Exception error)
        {
            try
            {
                var response = client.GetAsync(_options.DownloadUrl).GetAwaiter().GetResult();
                stream = response.Content.ReadAsStream();
                error = null;
                return true;
            }
            catch (HttpRequestException e)
            {
                stream = null;
                error = e;
                return false;
            }
            catch (TaskCanceledException e)
            {
                stream = null;
                error = e;
                return false;
            }
        }

        /// <summary>
        /// Append a public suffix to the internal list
        /// </summary>
        /// <param name="line"></param>
        private void AppendSuffix(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Length < 2)
            {
                // empty line
                return;
            }

            line = line.Trim();

            if (line[0] == '/' && line[1] == '/')
            {
                // comment
                return;
            }

            _list.Add(line);
        }

        private void PopulateLookupTable()
        {
            _lookupTable = [];

            foreach (var item in _list)
            {
                var asciiItem = _idnMapping.GetAscii(item);
                var domains = asciiItem.ToUpperInvariant().Split('.', StringSplitOptions.RemoveEmptyEntries);

                var tld = domains[^1];
                var keyExists = _lookupTable.TryGetValue(tld, out var upperLevelDomains);

                if (domains.Length == 1)
                {
                    if (keyExists)
                    {
                        continue;
                    }
                    else
                    {
                        _lookupTable.Add(tld, null);
                    }
                }
                else
                {
                    var head = asciiItem[..^(tld.Length + 1)].ToUpperInvariant();
                    if (keyExists)
                    {
                        if (upperLevelDomains == null)
                        {
                            _lookupTable[tld] = [head];
                        }
                        else
                        {
                            upperLevelDomains.Add(head);
                        }
                    }
                    else
                    {
                        _lookupTable.Add(tld, [head]);
                    }
                }
            }
        }
    }
}

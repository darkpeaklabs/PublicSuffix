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
        private PublicSuffixRule _rules;

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

            var originalDomains = domainName.Split('.', StringSplitOptions.RemoveEmptyEntries);

            var lookupDomains = _idnMapping.GetAscii(domainName)
                .ToUpperInvariant()
                .Split('.', StringSplitOptions.RemoveEmptyEntries);

            var rule = _rules;

            // If a hostname matches more than one rule in the file, the longest matching rule (the one with the most levels) will be used.
            for (var i = lookupDomains.Length - 1; i >= 0; i--) 
            {
                var domain = lookupDomains[i];

                if (!rule.SubDomains.TryGetValue(domain, out var subRule))
                {
                    if (rule == _rules)
                    {
                        // TLD does not match any suffix
                        throw new PublicSuffixNotFoundException($"Top-level domain of {domainName} does not match any public suffix");
                    }

                    if (i > 0)
                    {
                        return string.Join(".", originalDomains[i..]);
                    }
                    return domainName;
                }

                rule = subRule;

                if (subRule.IsWildCard)
                {
                    // current node is a wildcard
                    if (i <= 1)
                    {
                        // if there is only 1 or no domain to compare
                        // return input value
                        return domainName;
                    }
                    else if (subRule.SubDomains.Count > 0 && subRule.SubDomains.ContainsKey(lookupDomains[i - 1]))
                    {
                        // if there are lower level rules matching next domain, continue looking for apex domain 
                        continue;
                    }

                    if (subRule.WildCardExceptions.Count > 0 && subRule.WildCardExceptions.Contains(lookupDomains[i - 1]))
                    {
                        // if next domain matches an wildcard exception, return 
                        return string.Join(".", originalDomains[(i - 1)..]);
                    }

                    if (i > 2)
                    {
                        return string.Join(".", originalDomains[(i - 2)..]);
                    }

                    // return original input domain for all other cases
                    return domainName;
                }

            }

            return domainName;
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
                ReleaseLock(fileStream);
            }
        }

        private void ReleaseLock(FileStream fileStream)
        {
            PublicSuffixUtils.ReleaseFileLock(fileStream);
            _logger.LogFileLockReleased(_options.FilePath);
        }

        private FileStream AcquireLock()
        {
            PublicSuffixUtils.CreateDirectoryIfNotExists(Path.GetDirectoryName(_options.FilePath));

            _logger.LogAcquiringFileLock(_options.FilePath);
            var fileStream = PublicSuffixUtils.AcquireFileLock(_options.FilePath);
            _logger.LogFileLockAcquired(_options.FilePath);

            return fileStream;
        }

        /// <summary>
        /// Initialize the public suffix list when a new class instance is created
        /// </summary>
        private void InitializeList()
        {
            using var fileStream = AcquireLock();

            if (fileStream.Length > 10)
            {
                _timestamp = File.GetLastWriteTimeUtc(_options.FilePath);
                _logger.LogFoundExistingFile(_options.FilePath, fileStream.Length, _timestamp.Value);
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
                    _logger.LogDataInitializedFromResource();
                }
            }
            else
            {
                using StreamReader reader = new(stream: fileStream, encoding: Encoding.UTF8, leaveOpen: true);
                ReadList(reader, null);
            }

            ReleaseLock(fileStream);
        }

        private void ReadList(StreamReader reader, Action<string> lineAction)
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                AppendSuffix(line);
                lineAction?.Invoke(line);
            }
            UpdateRuleTree();
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

        /// <summary>
        /// Get content stream for download url from options
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        /// <exception cref="PublicSuffixListDownloadException"></exception>
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
                    _logger?.LogDataFileDownloadFailed(_options.DownloadUrl, error.Message);
                    throw new PublicSuffixListDownloadException(error.Message, error);
                }

                Thread.Sleep(TimeSpan.FromSeconds(attempt));
            }
            while (true);
        }

        /// <summary>
        /// Attempts to get HTTP response content stream
        /// </summary>
        /// <param name="client"></param>
        /// <param name="stream"></param>
        /// <param name="error"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Updates internal rule tree
        /// </summary>
        private void UpdateRuleTree()
        {
            _rules = new(".");
            PublicSuffixRule rule = null;

            foreach (var suffix in _list)
            {
                // convert suffix to ASCII
                var asciiSuffix = _idnMapping.GetAscii(suffix);

                // split suffix to domains
                var domains = asciiSuffix.ToUpperInvariant().Split('.', StringSplitOptions.RemoveEmptyEntries);

                rule = _rules;
                for (int i = domains.Length - 1; i >= 0; i--)
                {
                    var domain = domains[i];

                    // Rules: https://github.com/publicsuffix/list/wiki/Format#format
                    if (domain.Length == 1 && domain[0] == '*')
                    {
                        rule.IsWildCard = true;
                        break;
                    }
                    else if (domain[0] == '!')
                    {
                        // An exclamation mark (!) at the start of a rule marks an exception to a previous wildcard rule. An exception rule takes priority over any other matching rule.
                        rule.WildCardExceptions.Add(domain[1..]);
                        break;
                    }

                    if (!rule.SubDomains.TryGetValue(domain, out var childNode))
                    {
                        childNode = new PublicSuffixRule(domain);
                        rule.SubDomains.Add(domain, childNode);
                    }
                    rule = childNode;
                }
            }
        }
    }
}

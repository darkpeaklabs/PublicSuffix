
using Microsoft.Extensions.Logging;

namespace DarkPeakLabs.PublicSuffix.Test
{
    public class PublicSuffixTest : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<PublicSuffixTest> _logger;

        public PublicSuffixTest()
        {
            _loggerFactory = LoggerFactory.Create((builder) =>
            {
                builder
                    .AddDebug()
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });

            _logger = _loggerFactory.CreateLogger<PublicSuffixTest>();
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
        }

        [Fact]
        public void TestDownloadInitialization()
        {
            var options = new PublicSuffixListOptions() 
            {
                AutoUpdate = true,
                UpdateInterval = TimeSpan.FromSeconds(30),
            };

            ClearData(options);

            PublicSuffixList list = new(options, _loggerFactory);
            int count = list.Count;

            Assert.True(count > 0);
            Assert.True(File.Exists(options.FilePath));

            var lastWriteTime = File.GetLastWriteTimeUtc(options.FilePath);

            list = new(options, _loggerFactory);
            Assert.Equal(count, list.Count);
            Assert.Equal(lastWriteTime, File.GetLastWriteTimeUtc(options.FilePath));

            Thread.Sleep(options.UpdateInterval + TimeSpan.FromSeconds(3));

            list = new(options, _loggerFactory);
            Assert.Equal(count, list.Count);
            Assert.True(lastWriteTime < File.GetLastWriteTimeUtc(options.FilePath));
        }

        [Fact]
        public void TestResourceInitialization()
        {
            var options = new PublicSuffixListOptions()
            {
                AutoUpdate = false,
                UpdateInterval = TimeSpan.FromSeconds(30)
            };

            ClearData(options);

            PublicSuffixList list = new(options, _loggerFactory);
            int count = list.Count;

            Assert.True(count > 0);
            Assert.True(File.Exists(options.FilePath));
            var lastWriteTime = File.GetLastWriteTimeUtc(options.FilePath);

            Thread.Sleep(options.UpdateInterval + TimeSpan.FromSeconds(3));

            list = new(options, _loggerFactory);
            Assert.Equal(count, list.Count);
            Assert.True(lastWriteTime == File.GetLastWriteTimeUtc(options.FilePath));
        }

        [Fact]
        public void TestDownloadError()
        {
            var options = new PublicSuffixListOptions()
            {
                AutoUpdate = true,
                DownloadUrl = new Uri($"https://{Guid.NewGuid()}.test/data.txt"),
                DownloadTimeout = TimeSpan.FromSeconds(5)
            };

            ClearData(options);

            Assert.Throws<PublicSuffixListDownloadException>(() => 
            {
                PublicSuffixList list = new(options, _loggerFactory);
                int count = list.Count;
            });
        }

        [Fact]
        public void TestWildcardRule()
        {
            PublicSuffixList list = new(_loggerFactory);
            Assert.Contains("*.bd", list);
            Assert.Contains("*.nom.br", list);
            Assert.DoesNotContain("any.bd", list);
            Assert.DoesNotContain("any.nom.br", list);

            Assert.True(!list.Any(x => x[0] == '!' && x.EndsWith(".bd")));
            Assert.True(!list.Any(x => x[0] == '!' && x.EndsWith(".nom.br")));

            Assert.Equal("bd", list.GetDomainApex("bd"));
            Assert.Equal("any.bd", list.GetDomainApex("any.bd"));
            Assert.Equal("test.any.bd", list.GetDomainApex("test.any.bd"));
            Assert.Equal("test.any.bd", list.GetDomainApex("sub.test.any.bd"));
            Assert.Equal("test.any.bd", list.GetDomainApex("sub1.sub2.test.any.bd"));

            Assert.Equal("br", list.GetDomainApex("br"));
            Assert.Equal("any.br", list.GetDomainApex("any.br"));
            Assert.Equal("any.nom.br", list.GetDomainApex("any.nom.br"));
            Assert.Equal("test.any.nom.br", list.GetDomainApex("test.any.nom.br"));
            Assert.Equal("test.any.nom.br", list.GetDomainApex("sub.test.any.nom.br"));
            Assert.Equal("test.any.nom.br", list.GetDomainApex("sub1.sub2.test.any.nom.br"));
        }

        [Fact]
        public void TestExemptionRule()
        {
            PublicSuffixList list = new(_loggerFactory);

            Assert.Contains("*.ck", list);
            Assert.Contains("!www.ck", list);
            Assert.DoesNotContain("any.ck", list);
            Assert.Contains("*.kawasaki.jp", list);
            Assert.Contains("!city.kawasaki.jp", list);

            Assert.Equal("any.ck", list.GetDomainApex("any.ck"));

            Assert.Equal("test.any.ck", list.GetDomainApex("test.any.ck"));
            Assert.Equal("test.any.ck", list.GetDomainApex("sub.test.any.ck"));
            Assert.Equal("test.any.ck", list.GetDomainApex("sub1.sub2.test.any.ck"));

            Assert.Equal("kawasaki.jp", list.GetDomainApex("kawasaki.jp"));
            Assert.Equal("any.kawasaki.jp", list.GetDomainApex("any.kawasaki.jp"));
            Assert.Equal("test.any.kawasaki.jp", list.GetDomainApex("test.any.kawasaki.jp"));
            Assert.Equal("test.any.kawasaki.jp", list.GetDomainApex("sub.test.any.kawasaki.jp"));
            Assert.Equal("test.any.kawasaki.jp", list.GetDomainApex("sub1.sub2.test.any.kawasaki.jp"));

            Assert.Equal("city.kawasaki.jp", list.GetDomainApex("city.kawasaki.jp"));
            Assert.Equal("city.kawasaki.jp", list.GetDomainApex("test.city.kawasaki.jp"));
            Assert.Equal("city.kawasaki.jp", list.GetDomainApex("sub.city.kawasaki.jp"));
            Assert.Equal("city.kawasaki.jp", list.GetDomainApex("sub1.sub2.city.kawasaki.jp"));
        }

        [Fact]
        public void TestRuleNotFound()
        {
            PublicSuffixList list = new(_loggerFactory);

            Assert.DoesNotContain("akjshd", list);

            Assert.Throws<PublicSuffixNotFoundException>(() => list.GetDomainApex("akjshd"));
            Assert.Throws<PublicSuffixNotFoundException>(() => list.GetDomainApex("test.akjshd"));
            Assert.Throws<PublicSuffixNotFoundException>(() => list.GetDomainApex("sub.test.akjshd"));
        }

        [Fact]
        public void TestSimpleRule()
        {
            PublicSuffixList list = new(_loggerFactory);
            Assert.Contains("bb", list);
            Assert.DoesNotContain("test.bb", list);
            Assert.DoesNotContain("*.bb", list);

            Assert.Contains("biz.bb", list);
            Assert.DoesNotContain("*.biz.bb", list);
            Assert.DoesNotContain("test.biz.bb", list);

            Assert.Contains("am.gov.br", list);
            Assert.DoesNotContain("*.gov.br", list);
            Assert.DoesNotContain("*.am.gov.br", list);
            Assert.DoesNotContain("test.am.gov.br", list);

            Assert.Equal("bb", list.GetDomainApex("bb"));
            Assert.Equal("test.bb", list.GetDomainApex("test.bb"));
            Assert.Equal("test.bb", list.GetDomainApex("sub.test.bb"));
            Assert.Equal("test.bb", list.GetDomainApex("sub1.sub2.test.bb"));

            Assert.Equal("biz.bb", list.GetDomainApex("biz.bb"));
            Assert.Equal("test.biz.bb", list.GetDomainApex("test.biz.bb"));
            Assert.Equal("test.biz.bb", list.GetDomainApex("sub.test.biz.bb"));
            Assert.Equal("test.biz.bb", list.GetDomainApex("sub1.sub2.test.biz.bb"));

            Assert.Equal("am.gov.br", list.GetDomainApex("am.gov.br"));
            Assert.Equal("test.am.gov.br", list.GetDomainApex("test.am.gov.br"));
            Assert.Equal("test.am.gov.br", list.GetDomainApex("sub.test.am.gov.br"));
            Assert.Equal("test.am.gov.br", list.GetDomainApex("sub1.sub2.test.am.gov.br"));
        }

        [Fact]
        public void TestPickLongestRule()
        {
            PublicSuffixList list = new(_loggerFactory);
            Assert.Contains("schools.nsw.edu.au", list);
            Assert.Contains("nsw.edu.au", list);
            Assert.DoesNotContain("test.nsw.edu.au", list);

            Assert.Equal("test.nsw.edu.au", list.GetDomainApex("test.nsw.edu.au"));
            Assert.Equal("test.nsw.edu.au", list.GetDomainApex("sub1.sub2.test.nsw.edu.au"));

            Assert.Equal("test.schools.nsw.edu.au", list.GetDomainApex("test.schools.nsw.edu.au"));
            Assert.Equal("test.schools.nsw.edu.au", list.GetDomainApex("sub1.sub2.test.schools.nsw.edu.au"));
        }

        [Fact]
        public void TestMultipleWildCardRule()
        {
            PublicSuffixList list = new(_loggerFactory);

            Assert.Contains("*.futurecms.at", list);
            Assert.Contains("*.ex.futurecms.at", list);

            Assert.Equal("futurecms.at", list.GetDomainApex("futurecms.at"));
            Assert.Equal("any.futurecms.at", list.GetDomainApex("any.futurecms.at"));
            Assert.Equal("test.any.futurecms.at", list.GetDomainApex("test.any.futurecms.at"));
            Assert.Equal("test.any.futurecms.at", list.GetDomainApex("sub.test.any.futurecms.at"));

            Assert.Equal("ex.futurecms.at", list.GetDomainApex("ex.futurecms.at"));
            Assert.Equal("any.ex.futurecms.at", list.GetDomainApex("any.ex.futurecms.at"));
            Assert.Equal("test.any.ex.futurecms.at", list.GetDomainApex("test.any.ex.futurecms.at"));
            Assert.Equal("test.any.ex.futurecms.at", list.GetDomainApex("sub.test.any.ex.futurecms.at"));
            Assert.Equal("test.any.ex.futurecms.at", list.GetDomainApex("sub1.sub2.test.any.ex.futurecms.at"));
        }

        [Fact]
        public void TestGetDomainApex()
        {
            PublicSuffixList list = new(_loggerFactory);

            foreach (var suffix in list) 
            {
                _logger.LogInformation("Testing suffix {Suffix}", suffix);

                if (suffix[0] == '*')
                {
                    Assert.True(suffix.Length > 1 && suffix[1] == '.');

                    var apex = $"apex.any.{suffix[2..]}";

                    Assert.Equal(apex, list.GetDomainApex(apex));
                    Assert.Equal(apex, list.GetDomainApex($"subdomain.{apex}"));
                    Assert.Equal(apex, list.GetDomainApex($"sub1.sub2.{apex}"));
                }
                else if (suffix[0] == '!')
                {
                    var apex = suffix[1..];

                    Assert.Equal(apex, list.GetDomainApex(apex));
                    Assert.Equal(apex, list.GetDomainApex($"subdomain.{apex}"));
                    Assert.Equal(apex, list.GetDomainApex($"sub1.sub2.{apex}"));
                }
                else if (!char.IsLetterOrDigit(suffix[0]))
                {
                    _logger.LogWarning("Suffix {Suffix} does not start with a letter", suffix);
                }
                else
                {
                    var apex = $"apex.{suffix}";

                    Assert.Equal(apex, list.GetDomainApex(apex));
                    Assert.Equal(apex, list.GetDomainApex($"subdomain.{apex}"));
                    Assert.Equal(apex, list.GetDomainApex($"sub1.sub2.{apex}"));
                }
            }
        }

        private void ClearData(PublicSuffixListOptions options)
        {
            var appPath = Path.GetDirectoryName(options.FilePath);

            if (File.Exists(options.FilePath))
            {
                File.Delete(options.FilePath);
            }

            if (Directory.Exists(appPath))
            {
                Directory.Delete(appPath, true);
            }
        }
    }
}
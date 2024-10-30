using System;
using System.IO;
using System.Threading;

namespace DarkPeakLabs.PublicSuffix
{
    internal static class PublicSuffixUtils
    {
        private static readonly TimeSpan pollingInterval = TimeSpan.FromMilliseconds(300);
        private const int MaxAttempts = 3;

        public static void CreateDirectoryIfNotExists(string path)
        {
            int attempt = 0;
            while(!Directory.Exists(path))
            {
                try
                {
                    attempt++;
                    Directory.CreateDirectory(path);
                }
                catch (IOException e)
                {
                    if (attempt >= MaxAttempts)
                    {
                        throw new PublicSuffixException($"Unable to create folder {path}: {e.Message}", e);
                    }
                    Thread.Sleep(pollingInterval);
                }
            }
        }

        public static FileStream AcquireFileLock(string path)
        {
            do
            {
                if (TryAcquireFileLock(path, out var fileStream))
                {
                    return fileStream;
                }
                Thread.Sleep(pollingInterval);
            }
            while (true);
        }

        public static void ReleaseFileLock(FileStream fileStream)
        {
            fileStream.Close();
        }

        public static bool TryAcquireFileLock(string path, out FileStream fileStream)
        {
            try
            {
                fileStream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                fileStream = null;
                return false;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Omnius.Core;
using Omnius.Core.Io;
using Omnius.Lxna.Components.Internal;
using Omnius.Lxna.Components.Internal.Helpers;
using SevenZipExtractor;

namespace Omnius.Lxna.Components
{
    public sealed class ArchiveFileExtractor : DisposableBase, IArchiveFileExtractor
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private static readonly HashSet<string> _archiveFileExtensionList = new() { ".zip", ".rar", ".7z" };

        private readonly string _archiveFilePath;
        private readonly IBytesPool _bytesPool;

        private ArchiveFile _archiveFile = null!;
        private readonly Dictionary<string, Entry> _fileEntryMap = new();
        private readonly HashSet<string> _dirSet = new();

        internal sealed class ArchiveFileExtractorFactory : IArchiveFileExtractorFactory
        {
            public async ValueTask<IArchiveFileExtractor> CreateAsync(ArchiveFileExtractorOptions options, CancellationToken cancellationToken = default)
            {
                var result = new ArchiveFileExtractor(options);
                await result.InitAsync(cancellationToken);

                return result;
            }
        }

        public static IArchiveFileExtractorFactory Factory { get; } = new ArchiveFileExtractorFactory();

        internal ArchiveFileExtractor(ArchiveFileExtractorOptions options)
        {
            _archiveFilePath = options.ArchiveFilePath;
            _bytesPool = options.BytesPool;
        }

        internal async ValueTask InitAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            _archiveFile = new ArchiveFile(_archiveFilePath);
            this.ComputeFilesAndDirs(cancellationToken);
        }

        private void ComputeFilesAndDirs(CancellationToken cancellationToken = default)
        {
            try
            {
                using var canceller = cancellationToken.Register(() => _archiveFile.Dispose());

                foreach (var entry in _archiveFile.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry.IsFolder)
                    {
                        var dirPath = PathHelper.Normalize(entry.FileName);
                        _dirSet.Add(dirPath);
                    }
                    else
                    {
                        var filePath = PathHelper.Normalize(entry.FileName);
                        _fileEntryMap[filePath] = entry;
                    }
                }

                foreach (var filePath in _fileEntryMap.Keys)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var dirPath in PathHelper.ExtractDirectories(filePath))
                    {
                        _dirSet.Add(dirPath);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.Warn(e);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        protected override void OnDispose(bool disposing)
        {
            _archiveFile?.Dispose();
            _fileEntryMap.Clear();
            _dirSet.Clear();
        }

        public async ValueTask<bool> ExistsFileAsync(string path, CancellationToken cancellationToken = default)
        {
            return _fileEntryMap.ContainsKey(path);
        }

        public async ValueTask<bool> ExistsDirectoryAsync(string path, CancellationToken cancellationToken = default)
        {
            return _dirSet.Contains(path);
        }

        public async ValueTask<DateTime> GetFileLastWriteTimeAsync(string path, CancellationToken cancellationToken = default)
        {
            if (_fileEntryMap.TryGetValue(path, out var entry))
            {
                return entry.LastWriteTime.ToUniversalTime();
            }

            throw new FileNotFoundException();
        }

        public async ValueTask<IEnumerable<string>> FindDirectoriesAsync(string path, CancellationToken cancellationToken = default)
        {
            var results = new List<string>();

            foreach (var dirPath in _dirSet)
            {
                if (PathHelper.IsCurrentDirectory(path, dirPath))
                {
                    results.Add(dirPath);
                }
            }

            return results;
        }

        public async ValueTask<IEnumerable<string>> FindArchiveFilesAsync(string path, CancellationToken cancellationToken = default)
        {
            var results = new List<string>();

            foreach (var filePath in _fileEntryMap.Keys)
            {
                if (!_archiveFileExtensionList.Contains(Path.GetExtension(filePath))) continue;

                if (PathHelper.IsCurrentDirectory(path, filePath))
                {
                    results.Add(filePath);
                }
            }

            return results;
        }

        public async ValueTask<IEnumerable<string>> FindFilesAsync(string path, CancellationToken cancellationToken = default)
        {
            var results = new List<string>();

            foreach (var filePath in _fileEntryMap.Keys)
            {
                if (PathHelper.IsCurrentDirectory(path, filePath))
                {
                    results.Add(filePath);
                }
            }

            return results;
        }

        public async ValueTask<Stream> GetFileStreamAsync(string path, CancellationToken cancellationToken = default)
        {
            if (_fileEntryMap.TryGetValue(path, out var entry))
            {
                var memoryStream = new RecyclableMemoryStream(_bytesPool);

                using (var cancellableStream = new CancellableStream(memoryStream, cancellationToken, true))
                {
                    entry.Extract(cancellableStream);
                    await cancellableStream.FlushAsync(cancellationToken);
                }

                memoryStream.Seek(0, SeekOrigin.Begin);
                return memoryStream;
            }

            throw new FileNotFoundException();
        }

        public async ValueTask<long> GetFileSizeAsync(string path, CancellationToken cancellationToken = default)
        {
            if (_fileEntryMap.TryGetValue(path, out var entry))
            {
                return (long)entry.Size;
            }

            throw new FileNotFoundException();
        }

        public async ValueTask ExtractFileAsync(string path, Stream stream, CancellationToken cancellationToken = default)
        {
            if (_fileEntryMap.TryGetValue(path, out var entry))
            {
                using var cancellableStream = new CancellableStream(stream, cancellationToken, true);
                entry.Extract(cancellableStream);
                await cancellableStream.FlushAsync(cancellationToken);

                return;
            }

            throw new FileNotFoundException();
        }
    }
}

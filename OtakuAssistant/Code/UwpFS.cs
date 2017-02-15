using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

using OtakuLib;

namespace OtakuAssistant
{
    class UWPFS : PortableFS
    {
        private static readonly char[] Separators = {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
        };

        public Task<Stream> GetFileReadStream(string folderPath)
        {
            return GetFileStreamReadAsync(folderPath);
        }

        public Task<IReadOnlyList<Stream>> GetFilesReadStreams(string folderPath)
        {
            return GetFilesReadStreamsAsync(folderPath);
        }

        public Task<Stream> GetFileWriteStream(string folderPath)
        {
            return GetFileStreamWriteAsync(folderPath);
        }

        public Task<IReadOnlyList<Stream>> GetFilesWriteStreams(string folderPath)
        {
            return GetFilesReadWriteAsync(folderPath);
        }

        private async Task<Stream> GetFileStreamReadAsync(string filePath)
        {
            StorageFolder folder = Package.Current.InstalledLocation;
            foreach (string subfolder in Path.GetDirectoryName(filePath).Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                folder = await folder.GetFolderAsync(subfolder);
            }

            IStorageFile file = await folder.GetFileAsync(Path.GetFileName(filePath));
            return await file.OpenStreamForReadAsync();
        }

        private async Task<IReadOnlyList<Stream>> GetFilesReadStreamsAsync(string folderPath)
        {
            StorageFolder folder = Package.Current.InstalledLocation;
            foreach (string subfolder in folderPath.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                folder = await folder.GetFolderAsync(subfolder);
            }

            List<Stream> streams = new List<Stream>();
            foreach (IStorageFile file in await folder.GetFilesAsync())
            {
                streams.Add(await file.OpenStreamForReadAsync());
            }

            return streams;
        }
        
        private async Task<Stream> GetFileStreamWriteAsync(string filePath)
        {
            // TODO: create folders/file if not exists
            StorageFolder folder = Package.Current.InstalledLocation;
            foreach (string subfolder in Path.GetDirectoryName(filePath).Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                folder = await folder.GetFolderAsync(subfolder);
            }

            IStorageFile file = await folder.GetFileAsync(Path.GetFileName(filePath));
            return await file.OpenStreamForWriteAsync();
        }

        private async Task<IReadOnlyList<Stream>> GetFilesReadWriteAsync(string folderPath)
        {
            // TODO: create folders/file if not exists
            StorageFolder folder = Package.Current.InstalledLocation;
            foreach (string subfolder in folderPath.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                folder = await folder.GetFolderAsync(subfolder);
            }

            List<Stream> streams = new List<Stream>();
            foreach (IStorageFile file in await folder.GetFilesAsync())
            {
                streams.Add(await file.OpenStreamForWriteAsync());
            }

            return streams;
        }
    }
}

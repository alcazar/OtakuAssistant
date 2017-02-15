using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OtakuLib;

namespace DictionaryBinarizer
{
    class DotNetFS : PortableFS
    {
        public Task<Stream> GetFileReadStream(string filePath)
        {
            return Task.FromResult((Stream)File.OpenRead(filePath));
        }

        public Task<IReadOnlyList<Stream>> GetFilesReadStreams(string folderPath)
        {
            List<Stream> streams = new List<Stream>();
            foreach (string filePath in Directory.EnumerateFiles(folderPath))
            {
                streams.Add(File.OpenRead(filePath));
            }

            return Task.FromResult((IReadOnlyList<Stream>)streams);
        }

        public Task<Stream> GetFileWriteStream(string filePath)
        {
            return Task.FromResult((Stream)File.OpenWrite(filePath));
        }

        public Task<IReadOnlyList<Stream>> GetFilesWriteStreams(string folderPath)
        {
            List<Stream> streams = new List<Stream>();
            foreach (string filePath in Directory.EnumerateFiles(folderPath))
            {
                streams.Add(File.OpenWrite(filePath));
            }

            return Task.FromResult((IReadOnlyList<Stream>)streams);
        }
    }
}

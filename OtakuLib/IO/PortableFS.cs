using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OtakuLib
{
    public interface PortableFS
    {
        Task<Stream> GetFileReadStream(string filePath);
        Task<IReadOnlyList<Stream>> GetFilesReadStreams(string folderPath);
        
        Task<Stream> GetFileWriteStream(string filePath);
        Task<IReadOnlyList<Stream>> GetFilesWriteStreams(string folderPath);
    }
}

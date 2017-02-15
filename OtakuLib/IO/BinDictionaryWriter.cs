using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OtakuLib
{
    public class BinDictionaryWriter
    {
        public static async Task WriteDictionary(string dictionaryName, WordDictionary dictionary, PortableFS portableFS)
        {
            const int sliceSize = 20000;
            int jobSliceStart = 0;
            int jobSliceEnd = sliceSize;
            
            List<Task<MemoryStream>> dictionaryWriteJobs = new List<Task<MemoryStream>>();
            while (jobSliceStart < dictionary.Count)
            {
                BinDictionaryWriteJob dictionaryWriteJob = new BinDictionaryWriteJob(dictionary, jobSliceStart, Math.Min(jobSliceEnd, dictionary.Count));

                dictionaryWriteJobs.Add(Task.Run((Func<MemoryStream>)dictionaryWriteJob.WriteDictionaryPart));

                jobSliceStart = jobSliceEnd;
                jobSliceEnd += sliceSize;
            }

            Task.WaitAll(dictionaryWriteJobs.ToArray());

            Stream stream = await portableFS.GetFileWriteStream(Path.Combine("Dictionaries", dictionaryName + ".otakudict"));

            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);

            // version
            writer.Write((int)0);
            // number of parts
            writer.Write((int)dictionaryWriteJobs.Count);

            // size of each part
            foreach (Task<MemoryStream> dictionaryWriteJob in dictionaryWriteJobs)
            {
                writer.Write((int)dictionaryWriteJob.Result.Length);
            }
            
            // copy parts
            foreach (Task<MemoryStream> dictionaryWriteJob in dictionaryWriteJobs)
            {
                dictionaryWriteJob.Result.Seek(0, SeekOrigin.Begin);
                dictionaryWriteJob.Result.CopyTo(stream);
                dictionaryWriteJob.Result.Dispose();
            }

            stream.Flush();
            stream.Dispose();
        }

        private class BinDictionaryWriteJob
        {
            private WordDictionary Dictionary;
            private int JobSliceStart;
            private int JobSliceEnd;

            public BinDictionaryWriteJob(WordDictionary dictionary, int jobSliceStart, int jobSliceEnd)
            {
                Dictionary = dictionary;
                JobSliceStart = jobSliceStart;
                JobSliceEnd = jobSliceEnd;
            }

            public MemoryStream WriteDictionaryPart()
            {
                MemoryStream stream = new MemoryStream();

                BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);

                for (int w = JobSliceStart; w < JobSliceEnd; ++w)
                {
                    Word word = Dictionary[w];

                    writer.Write(word.Hanzi);
                    writer.Write(word.Traditional ?? string.Empty);
                    writer.Write(word.Link ?? string.Empty);
                    writer.Write(word.ThumbPinyin ?? string.Empty);
                    writer.Write(word.ThumbTranslation ?? string.Empty);

                    writer.Write((byte)word.Meanings.Length);
                    foreach (Meaning meaning in word.Meanings)
                    {
                        writer.Write((byte)meaning.Pinyins.Length);
                        foreach (string pinyin in meaning.Pinyins)
                        {
                            writer.Write(pinyin);
                        }
                        writer.Write((byte)meaning.Translations.Length);
                        foreach (string translation in meaning.Translations)
                        {
                            writer.Write(translation);
                        }
                    }

                    if (word.Tags == null)
                    {
                        writer.Write((byte)0);
                    }
                    else
                    {
                        writer.Write((byte)word.Tags.Length);
                        foreach (string tag in word.Tags)
                        {
                            writer.Write(tag);
                        }
                    }
                }

                return stream;
            }
        }
    }
}

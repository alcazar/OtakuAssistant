using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OtakuLib
{
    public class BinDictionaryWriter
    {
        public static async Task WriteDictionary(string dictionaryName, PortableFS portableFS)
        {
            const int sliceSize = 20000;
            int jobSliceStart = 0;
            int jobSliceEnd = sliceSize;
            
            List<Task<MemoryStream>> dictionaryWriteJobs = new List<Task<MemoryStream>>();
            while (jobSliceStart < WordDictionary.Words.Count)
            {
                BinDictionaryWriteJob dictionaryWriteJob = new BinDictionaryWriteJob(jobSliceStart, Math.Min(jobSliceEnd, WordDictionary.Words.Count));

                dictionaryWriteJobs.Add(Task.Run((Func<MemoryStream>)dictionaryWriteJob.WriteDictionaryPart));

                jobSliceStart = jobSliceEnd;
                jobSliceEnd += sliceSize;
            }

            Task.WaitAll(dictionaryWriteJobs.ToArray());

            Stream stream = await portableFS.GetFileWriteStream(Path.Combine("Dictionaries", dictionaryName + ".otakudict"));

            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);

            // version
            writer.Write((int)3);
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

            // make sure we're at end
            writer.Seek(0, SeekOrigin.End);

            writer.Write((int)WordDictionary.IndexedWords.Length);
            writer.Write((int)WordDictionary.IndexedWordMatches.Length);

            // copy index
            foreach (IndexedWord indexedWord in WordDictionary.IndexedWords)
            {
                writer.Write(indexedWord.IndexedWordStr);
                writer.Write((ulong)indexedWord.LetterMask);
                writer.Write((int)indexedWord.MatchesCount);
            }

            foreach (int wordMatch in WordDictionary.IndexedWordMatches)
            {
                writer.Write(wordMatch);
            }

            stream.Flush();
            stream.Dispose();
        }

        private class BinDictionaryWriteJob
        {
            private int JobSliceStart;
            private int JobSliceEnd;

            public BinDictionaryWriteJob(int jobSliceStart, int jobSliceEnd)
            {
                JobSliceStart = jobSliceStart;
                JobSliceEnd = jobSliceEnd;
            }

            public MemoryStream WriteDictionaryPart()
            {
                MemoryStream stream = new MemoryStream();

                BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);

                for (int w = JobSliceStart; w < JobSliceEnd; ++w)
                {
                    Word word = WordDictionary.Words[w];

                    writer.Write(word.Hanzi);
                    writer.Write(word.Traditional);
                    writer.Write(word.Radicals);
                    writer.Write(word.Link);
                    writer.Write(word.ThumbPinyin);
                    writer.Write(word.ThumbTranslation);

                    writer.Write((byte)word.Meanings.Count);
                    foreach (Meaning meaning in word.Meanings)
                    {
                        writer.Write((byte)meaning.Pinyins.Count);
                        foreach (string pinyin in meaning.Pinyins)
                        {
                            writer.Write(pinyin);
                        }
                        writer.Write((byte)meaning.Translations.Count);
                        foreach (string translation in meaning.Translations)
                        {
                            writer.Write(translation);
                        }
                    }
                    
                    writer.Write((byte)word.Tags.Count);
                    foreach (string tag in word.Tags)
                    {
                        writer.Write(tag);
                    }

                    writer.Write((ulong)word.PinyinMask);
                }

                return stream;
            }
        }
    }
}

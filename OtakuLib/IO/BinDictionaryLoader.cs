using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OtakuLib
{
    public class BinDictionaryLoader : DictionaryLoader
    {
        public BinDictionaryLoader(string dictionaryName, PortableFS portableFS, bool buildThumbs = true) : base(dictionaryName, portableFS, buildThumbs)
        {
        }

        protected override async Task<List<DictionaryLoadJob>> GetDictionaryLoadJobs()
        {
            Stream stream = await FS.GetFileReadStream(Path.Combine("Dictionaries", DictionaryName + ".otakudict"));

            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, (int)stream.Length);
            stream.Dispose();

            MemoryStream memStream = new MemoryStream(buffer);

            BinaryReader reader = new BinaryReader(memStream, Encoding.UTF8);

            uint version = reader.ReadUInt32();
            Debug.Assert(version <= 0);

            uint partCount = reader.ReadUInt32();

            List<DictionaryLoadJob> dictionaryLoadJobs = new List<DictionaryLoadJob>();
            
            int offset = (int)(memStream.Position + partCount * sizeof(Int32));
            for (uint part = 0; part < partCount; ++part)
            {
                int size = reader.ReadInt32();

                MemoryStream workerStream = new MemoryStream(buffer, offset, size);
                dictionaryLoadJobs.Add(new BinDictionaryLoadJob(version, workerStream, LoadTaskCanceller.Token, BuildThumbs));

                offset += size;
            }

            Debug.Assert(offset == memStream.Length);

            memStream.Dispose();

            return dictionaryLoadJobs;
        }

        protected class BinDictionaryLoadJob : DictionaryLoadJob
        {
            protected uint Version;

            public BinDictionaryLoadJob(uint version, Stream stream, CancellationToken cancellationToken, bool buildThumbs)
                : base(stream, cancellationToken, buildThumbs)
            {
                Version = version;
            }

            private List<Str> AllPinyinsBuilder = new List<Str>();

            public override List<Word> LoadDictionaryPart()
            {
                List<Word> words = new List<Word>();

                BinaryReader reader = new BinaryReader(LoadStream, Encoding.UTF8);

                while(LoadStream.Position < LoadStream.Length)
                {
                    Word word = new Word();
                    word.Hanzi = reader.ReadString();
                    word.Traditional = reader.ReadString();
                    word.Link = reader.ReadString();
                    word.ThumbPinyin = reader.ReadString();
                    word.ThumbTranslation = reader.ReadString();

                    byte meaningCount = reader.ReadByte();
                    word.Meanings = new Meaning[meaningCount];
                    for (byte i = 0; i < meaningCount; ++i)
                    {
                        byte pinyinCount = reader.ReadByte();
                        word.Meanings[i] = new Meaning();
                        word.Meanings[i].Pinyins = new Str[pinyinCount];
                        for (byte j = 0; j < pinyinCount; ++j)
                        {
                            word.Meanings[i].Pinyins[j] = reader.ReadString();
                            AllPinyinsBuilder.Add(word.Meanings[i].Pinyins[j]);
                        }
                        byte translationCount = reader.ReadByte();
                        word.Meanings[i].Translations = new Str[translationCount];
                        for (byte j = 0; j < translationCount; ++j)
                        {
                            word.Meanings[i].Translations[j] = reader.ReadString();
                        }
                    }

                    byte tagCount = reader.ReadByte();
                    word.Tags = tagCount > 0 ? new string[tagCount] : null;
                    for (byte i = 0; i < tagCount; ++i)
                    {
                        word.Tags[i] = reader.ReadString();
                    }

                    if (word.Traditional.Length == 0)
                    {
                        word.Traditional = null;
                    }
                    if (word.Link.Length == 0)
                    {
                        word.Link = null;
                    }

                    if (word.ThumbPinyin.Length == 0)
                    {
                        word.ThumbPinyin = BuildThumb(AllPinyinsBuilder);
                    }
                    if (word.ThumbTranslation.Length == 0)
                    {
                        word.ThumbTranslation = BuildThumb(word.Meanings[0].Translations);
                    }

                    AllPinyinsBuilder.Clear();

                    words.Add(word);
                }

                return words;
            }
        }
    }
}

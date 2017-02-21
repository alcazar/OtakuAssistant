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
        protected override bool AlreadySorted { get { return true; } }

        public BinDictionaryLoader(string dictionaryName, PortableFS portableFS, bool buildThumbs = true) : base(dictionaryName, portableFS, buildThumbs)
        {
        }

        protected override async Task<List<DictionaryLoadJob>> GetDictionaryLoadJobs()
        {
            List<DictionaryLoadJob> dictionaryLoadJobs = new List<DictionaryLoadJob>();

            Stream stream = await FS.GetFileReadStream(Path.Combine("Dictionaries", DictionaryName + ".otakudict"));

            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, (int)stream.Length);
            stream.Dispose();

            MemoryStream memStream = new MemoryStream(buffer);
            BinaryReader reader = new BinaryReader(memStream, Encoding.UTF8);

            uint version = reader.ReadUInt32();
            Debug.Assert(version <= 1);
            
            uint partCount = reader.ReadUInt32();

            
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

            public override void LoadDictionaryPart()
            {
                BinaryReader reader = new BinaryReader(LoadStream, Encoding.UTF8);
                
                StringListMemoryBuilder TagsBuilder = new StringListMemoryBuilder();
                StringListMemoryBuilder PinyinsBuilder = new StringListMemoryBuilder();
                StringListMemoryBuilder TranslationsBuilder = new StringListMemoryBuilder();

                List<string> ThumbPinyinBuilder = new List<string>();
                List<string> ThumbTranslationBuilder = new List<string>();

                while(LoadStream.Position < LoadStream.Length)
                {
                    string hanzi = string.Empty;
                    string traditional = string.Empty;
                    string radicals = string.Empty;
                    string link = string.Empty;
                    string thumbPinyin = string.Empty;
                    string thumbTranslation = string.Empty;
                    
                    hanzi = reader.ReadString();
                    traditional = reader.ReadString();
                    if (Version == 1)
                    {
                        radicals = reader.ReadString();
                    }
                    link = reader.ReadString();
                    thumbPinyin = reader.ReadString();
                    thumbTranslation = reader.ReadString();

                    byte meaningCount = reader.ReadByte();
                    for (byte i = 0; i < meaningCount; ++i)
                    {
                        byte pinyinCount = reader.ReadByte();
                        for (byte j = 0; j < pinyinCount; ++j)
                        {
                            string pinyin = reader.ReadString();
                            PinyinsBuilder.Add(pinyin);
                            ThumbPinyinBuilder.Add(pinyin);
                        }
                        byte translationCount = reader.ReadByte();
                        for (byte j = 0; j < translationCount; ++j)
                        {
                            string translation = reader.ReadString();
                            TranslationsBuilder.Add(translation);
                            if (MeaningMemoryBuilder.MeaningMemory.Count == MeaningMemoryBuilder.MeaningStart)
                            {
                                ThumbTranslationBuilder.Add(translation);
                            }
                        }
                            
                        MeaningMemoryBuilder.Add(PinyinsBuilder, TranslationsBuilder);

                        PinyinsBuilder.Clear();
                        TranslationsBuilder.Clear();
                    }

                    byte tagCount = reader.ReadByte();
                    for (byte i = 0; i < tagCount; ++i)
                    {
                        TagsBuilder.Add(reader.ReadString());
                    }

                    if (thumbPinyin == string.Empty)
                    {
                        thumbPinyin = BuildThumb(ThumbPinyinBuilder);
                    }
                    if (thumbTranslation == string.Empty)
                    {
                        thumbTranslation = BuildThumb(ThumbTranslationBuilder);
                    }

                    Words.Add(new Word(StringMemoryBuilder, StringLengthMemoryBuilder,
                        hanzi, traditional, thumbPinyin, thumbTranslation, radicals, link,
                        MeaningMemoryBuilder, TagsBuilder));
                    
                    TagsBuilder.Clear();
                    MeaningMemoryBuilder.Clear();
                    ThumbPinyinBuilder.Clear();
                    ThumbTranslationBuilder.Clear();
                }
            }
        }
    }
}

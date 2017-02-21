using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;

namespace OtakuLib
{
    public class XmlDictionaryLoader : DictionaryLoader
    {
        public XmlDictionaryLoader(string dictionaryName, PortableFS portableFS, bool buildThumbs = true)
            : base(dictionaryName, portableFS, buildThumbs)
        {
        }

        protected override async Task<List<DictionaryLoadJob>> GetDictionaryLoadJobs()
        {
            List<DictionaryLoadJob> dictionaryLoadJobs = new List<DictionaryLoadJob>();

            foreach (Stream stream in await FS.GetFilesReadStreams(Path.Combine("Dictionaries", DictionaryName)))
            {
                dictionaryLoadJobs.Add(new XmlDictionaryLoadJob(stream, LoadTaskCanceller.Token, BuildThumbs));
            }

            return dictionaryLoadJobs;
        }

        protected class XmlDictionaryLoadJob : DictionaryLoadJob
        {
            private StringListMemoryBuilder TagsBuilder = new StringListMemoryBuilder();
            private StringListMemoryBuilder PinyinsBuilder = new StringListMemoryBuilder();
            private StringListMemoryBuilder TranslationsBuilder = new StringListMemoryBuilder();

            private List<string> ThumbPinyinBuilder = new List<string>();
            private List<string> ThumbTranslationBuilder = new List<string>();

            public XmlDictionaryLoadJob(Stream stream, CancellationToken cancellationToken, bool buildThumbs)
                : base(stream, cancellationToken, buildThumbs)
            {
            }

            public override void LoadDictionaryPart()
            {
                XmlReader reader = XmlReader.Create(LoadStream);
                
                while (reader.ReadToFollowing("Word"))
                {
                    Words.Add(BuildWord(reader));
                    JobCancellationToken.ThrowIfCancellationRequested();
                }

                reader.Dispose();
            }

            private enum Cursor
            {
                Traditional,
                Radicals,
                Link,
                ThumbPinyin,
                ThumbTranslation,
                Meaning,
                MeaningPinyin,
                MeaningTranslation,
                Tag,
            }

            private Word BuildWord(XmlReader reader)
            {
                string hanzi = string.Empty;
                string traditional = string.Empty;
                string radicals = string.Empty;
                string link = string.Empty;
                string thumbPinyin = string.Empty;
                string thumbTranslation = string.Empty;

                reader.ReadToFollowing("Hanzi");
                hanzi = reader.ReadElementContentAsString();

                Cursor cursor = Cursor.Traditional;
                int wordDepth = reader.Depth;
                while (reader.Read() && reader.Depth >= wordDepth)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        string name = reader.Name;

                        if (cursor <= Cursor.Traditional && name == "Traditional")
                        {
                            traditional = reader.ReadElementContentAsString();
                            cursor = Cursor.Traditional + 1;
                        }
                        else if (cursor <= Cursor.Radicals && name == "Radicals")
                        {
                            radicals = reader.ReadElementContentAsString();
                            cursor = Cursor.Radicals + 1;
                        }
                        else if (cursor <= Cursor.Link && name == "Link")
                        {
                            link = reader.ReadElementContentAsString();
                            cursor = Cursor.Link + 1;
                        }
                        else if (cursor <= Cursor.ThumbPinyin && name == "ThumbPinyin")
                        {
                            thumbPinyin = reader.ReadElementContentAsString();
                            cursor = Cursor.ThumbPinyin + 1;
                        }
                        else if (cursor <= Cursor.ThumbTranslation && name == "ThumbTranslation")
                        {
                            thumbTranslation = reader.ReadElementContentAsString();
                            cursor = Cursor.ThumbTranslation + 1;
                        }
                        else if (cursor <= Cursor.Meaning && name == "Meaning")
                        {
                            int meaningDepth = reader.Depth;
                            cursor = Cursor.ThumbPinyin;
                            while (reader.Read() && reader.Depth > meaningDepth)
                            {
                                if (reader.NodeType == XmlNodeType.Element)
                                {
                                    if (cursor <= Cursor.MeaningPinyin && reader.Name == "Pinyin")
                                    {
                                        string pinyin = reader.ReadElementContentAsString();
                                        PinyinsBuilder.Add(pinyin);
                                        ThumbPinyinBuilder.Add(pinyin);
                                    }
                                    else
                                    {
                                        cursor = Cursor.MeaningTranslation;
                                        string translation = reader.ReadElementContentAsString();
                                        TranslationsBuilder.Add(translation);
                                        if (MeaningMemoryBuilder.MeaningMemory.Count == MeaningMemoryBuilder.MeaningStart)
                                        {
                                            ThumbTranslationBuilder.Add(translation);
                                        }
                                    }
                                }
                            }
                            MeaningMemoryBuilder.Add(PinyinsBuilder, TranslationsBuilder);

                            PinyinsBuilder.Clear();
                            TranslationsBuilder.Clear();

                            cursor = Cursor.Meaning;
                        }
                        else// if (cursor <= Cursor.Tag && name == "Tag")
                        {
                            // tag is the last element, we do not need to check
                            cursor = Cursor.Tag;
                            TagsBuilder.Add(reader.ReadElementContentAsString());
                        }
                    }
                }

                if (thumbPinyin == string.Empty)
                {
                    thumbPinyin = BuildThumb(ThumbPinyinBuilder);
                }
                if (thumbTranslation == string.Empty)
                {
                    thumbTranslation = BuildThumb(ThumbTranslationBuilder);
                }

                Word word = new Word(StringMemoryBuilder, StringLengthMemoryBuilder,
                    hanzi, traditional, thumbPinyin, thumbTranslation, radicals, link,
                    MeaningMemoryBuilder, TagsBuilder);
            
                TagsBuilder.Clear();
                MeaningMemoryBuilder.Clear();
                ThumbPinyinBuilder.Clear();
                ThumbTranslationBuilder.Clear();

                return word;
            }
        }
    }
}

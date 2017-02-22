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
            public XmlDictionaryLoadJob(Stream stream, CancellationToken cancellationToken, bool buildThumbs)
                : base(stream, cancellationToken, buildThumbs)
            {
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

            public override void LoadDictionaryPart()
            {
                XmlReader reader = XmlReader.Create(LoadStream);
                
                StringPointerBuilder pinyinBuilder = new StringPointerBuilder();
                StringPointerBuilder translationBuilder = new StringPointerBuilder();
                StringPointerBuilder tagBuilder = new StringPointerBuilder();

                List<string> thumbPinyinBuilder = new List<string>();
                List<string> thumbTranslationBuilder = new List<string>();
            
                while (reader.ReadToFollowing("Word"))
                {
                    JobCancellationToken.ThrowIfCancellationRequested();

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
                                            pinyinBuilder.Add(pinyin);
                                            thumbPinyinBuilder.Add(pinyin);
                                        }
                                        else
                                        {
                                            cursor = Cursor.MeaningTranslation;
                                            string translation = reader.ReadElementContentAsString();
                                            translationBuilder.Add(translation);
                                            if (MeaningBuilder.MeaningMemory.Count == MeaningBuilder.MeaningStart)
                                            {
                                                thumbTranslationBuilder.Add(translation);
                                            }
                                        }
                                    }
                                }
                                MeaningBuilder.Add(pinyinBuilder, translationBuilder);

                                pinyinBuilder.Clear();
                                translationBuilder.Clear();

                                cursor = Cursor.Meaning;
                            }
                            else// if (cursor <= Cursor.Tag && name == "Tag")
                            {
                                // tag is the last element, we do not need to check
                                cursor = Cursor.Tag;
                                tagBuilder.Add(reader.ReadElementContentAsString());
                            }
                        }
                    }

                    if (thumbPinyin == string.Empty)
                    {
                        thumbPinyin = BuildThumb(thumbPinyinBuilder);
                    }
                    if (thumbTranslation == string.Empty)
                    {
                        thumbTranslation = BuildThumb(thumbTranslationBuilder);
                    }

                    Words.Add(new Word(StringBuilder,
                        hanzi, traditional, thumbPinyin, thumbTranslation, radicals, link,
                        MeaningBuilder, tagBuilder));
            
                    tagBuilder.Clear();
                    MeaningBuilder.Clear();
                    thumbPinyinBuilder.Clear();
                    thumbTranslationBuilder.Clear();
                }

                reader.Dispose();
            }
        }
    }
}

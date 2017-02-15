using System;
using System.Collections.Generic;
using System.IO;
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
            private List<string> TagsBuilder = new List<string>();
            private List<Str> PinyinsBuilder = new List<Str>();
            private List<Str> TranslationsBuilder = new List<Str>();
            private List<Meaning> MeaningsBuilder = new List<Meaning>();
            private List<Str> AllPinyinsBuilder = new List<Str>();

            public XmlDictionaryLoadJob(Stream stream, CancellationToken cancellationToken, bool buildThumbs)
                : base(stream, cancellationToken, buildThumbs)
            {

            }

            public override List<Word> LoadDictionaryPart()
            {
                XmlReader reader = XmlReader.Create(LoadStream);

                List<Word> words = new List<Word>();
            
                while (reader.ReadToFollowing("Word"))
                {
                    words.Add(BuildWord(reader));
                    JobCancellationToken.ThrowIfCancellationRequested();
                }

                reader.Dispose();

                return words;
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
                Word word = new Word();

                reader.ReadToFollowing("Name");
                word.Hanzi = reader.ReadElementContentAsString();

                Cursor cursor = Cursor.Traditional;
                int wordDepth = reader.Depth;
                while (reader.Read() && reader.Depth >= wordDepth)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        string name = reader.Name;

                        if (cursor <= Cursor.Traditional && name == "Traditional")
                        {
                            word.Traditional = reader.ReadElementContentAsString();
                            cursor = Cursor.Traditional + 1;
                        }
                        else if (cursor <= Cursor.Radicals && name == "Radicals")
                        {
                            word.Radicals = reader.ReadElementContentAsString();
                            cursor = Cursor.Radicals + 1;
                        }
                        else if (cursor <= Cursor.Link && name == "Link")
                        {
                            word.Link = reader.ReadElementContentAsString();
                            cursor = Cursor.Link + 1;
                        }
                        else if (cursor <= Cursor.ThumbPinyin && name == "ThumbPinyin")
                        {
                            word.ThumbPinyin = reader.ReadElementContentAsString();
                            cursor = Cursor.ThumbPinyin + 1;
                        }
                        else if (cursor <= Cursor.ThumbTranslation && name == "ThumbTranslation")
                        {
                            word.ThumbTranslation = reader.ReadElementContentAsString();
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
                                        AllPinyinsBuilder.Add(pinyin);
                                    }
                                    else
                                    {
                                        cursor = Cursor.MeaningTranslation;
                                        TranslationsBuilder.Add(reader.ReadElementContentAsString());
                                    }
                                }
                            }
                            Meaning meaning = new Meaning();
                            meaning.Pinyins = PinyinsBuilder.ToArray();
                            meaning.Translations = TranslationsBuilder.ToArray();
                            MeaningsBuilder.Add(meaning);

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

                word.Meanings = MeaningsBuilder.ToArray();

                // save space if no tags
                word.Tags = TagsBuilder.Count > 0 ? TagsBuilder.ToArray() : null;

                if (word.ThumbPinyin == null)
                {
                    word.ThumbPinyin = BuildThumb(AllPinyinsBuilder);
                }
                if (word.ThumbTranslation == null)
                {
                    word.ThumbTranslation = BuildThumb(MeaningsBuilder[0].Translations);
                }
            
                TagsBuilder.Clear();
                MeaningsBuilder.Clear();
                AllPinyinsBuilder.Clear();

                return word;
            }
        }
    }
}

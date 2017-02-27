using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OtakuLib;

namespace OtakuTest
{
    /// <summary>
    /// Summary description for OtakuTest
    /// </summary>
    [TestClass]
    public class OtakuTest
    {
        public OtakuTest()
        {
            if (!WordDictionary.Loading.IsCompleted)
            {
                Directory.SetCurrentDirectory("../../../OtakuAssistant");
                new BinDictionaryLoader("Cedict_CN_ENG", new DotNetFS());
            }
            Assert.IsTrue(WordDictionary.Loading.AsyncWaitHandle.WaitOne(5000));
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion
        
        private const string hanziSkip = @"○，";
        private string Sanitize(string str)
        {
            return string.Join("", str.Where((char c) =>
            {
                return c > 255 && !hanziSkip.Contains(c);
            }));
        }

        private void TestStringIsChinese(string str)
        {
            if (str == null)
            {
                return;
            }
            
            // remove false positive from entries containing unicode characters
            // and punctuation
            str = Sanitize(str);

            Assert.IsTrue(str.IsChinese(), string.Format("{0} {{{1}}}", str, string.Join(",", str.Select((char c) => { return "0x" + ((int)c).ToString("X6"); }))));
        }

        [TestMethod]
        /// <summary>
        /// Test that the IsChinese() extension method works properly
        /// Iterates over all the chinese simplified/traditional characters from the database and check it returns true
        /// </summary>
        public void TestStringIsChinese()
        {
            foreach (Word word in WordDictionary.Words)
            {
                TestStringIsChinese(word.Hanzi);
                TestStringIsChinese(word.Traditional);
            }
        }
        
        /// <summary>
        /// Test that the IsPinyin() extension method works properly
        /// Iterates over all the pinyins from the database and check it returns true
        /// </summary>
        [TestMethod]
        public void TestStringIsPinyin()
        {
            foreach (Word word in WordDictionary.Words)
            {
                // skip non chinese word as they may contain single letters like 3C with no pinyin
                if (word.Hanzi.IsChinese())
                {
                    foreach (Meaning meaning in word.Meanings)
                    {
                        foreach (string pinyin in meaning.Pinyins)
                        {
                            foreach (string pinyinPart in pinyin.Split(' '))
                            {
                                string _pinyinPart = pinyinPart.RemoveAccents();
                                // pinyins that can't be pronunced are marked as xx
                                // 儿 is marked as r in the database though it can't be alone (pronunced er alone)
                                // 呒 is marked as m but never occurs alone
                                if (_pinyinPart != "xx" && _pinyinPart != "r" && _pinyinPart != "m" && _pinyinPart != "yo")
                                {
                                    Assert.IsTrue(_pinyinPart.IsPinyin(), string.Format("{0} {1}", word.Hanzi, _pinyinPart));
                                }
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void TestPinyinAccents()
        {
            Word word = WordDictionary.Words["女"];
            Assert.AreEqual("nǚ, rǔ", word.ThumbPinyin);

            word = WordDictionary.Words["恧"];
            Assert.AreEqual("nǜ", word.ThumbPinyin);
            
            word = WordDictionary.Words["三略"];
            Assert.AreEqual("sān lüè", word.ThumbPinyin);

            word = WordDictionary.Words["晴"];
            Assert.AreEqual("qíng", word.ThumbPinyin);
        }

        [TestMethod]
        public void TestMultiMeaningEntry()
        {
            Word word = WordDictionary.Words["食"];

            MeaningList.Enumerator meaning = word.Meanings.GetEnumerator();
            StringList.Enumerator entry;

            meaning.MoveNext();

            entry = meaning.Current.Pinyins.GetEnumerator();
            entry.MoveNext();
            Assert.AreEqual("shí", entry.Current);
            
            entry = meaning.Current.Translations.GetEnumerator();
            entry.MoveNext();
            Assert.AreEqual("to eat", entry.Current);
            entry.MoveNext();
            Assert.AreEqual("food", entry.Current);
            entry.MoveNext();
            Assert.AreEqual("animal feed", entry.Current);
            entry.MoveNext();
            Assert.AreEqual("eclipse", entry.Current);
            
            meaning.MoveNext();

            entry = meaning.Current.Pinyins.GetEnumerator();
            entry.MoveNext();
            Assert.AreEqual("sì", entry.Current);
            
            entry = meaning.Current.Translations.GetEnumerator();
            entry.MoveNext();
            Assert.AreEqual("to feed", entry.Current);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OtakuLib;

namespace OtakuTest
{
    [TestClass]
    public class SearchTest
    {
        public SearchTest()
        {
            if (DictionaryLoader.Current == null)
            {
                Directory.SetCurrentDirectory("../../../OtakuAssistant");
                new BinDictionaryLoader("Cedict_CN_ENG", new DotNetFS());
            }
            Assert.IsTrue(DictionaryLoader.Current.LoadTask.Wait(1000));
        }

        private void RunSearchTest(string searchText, bool AnyOrder, params string[] expectedSearchResults)
        {
            WordSearch search = new WordSearch(searchText);
            Assert.IsTrue(search.SearchTask.Wait(1000));

            SearchResult searchResults = search.SearchTask.Result;
            Assert.IsTrue(searchResults.Count >= expectedSearchResults.Length, string.Format("Search {0} did not return enough results", searchText));
            
            searchResults.RemoveRange(expectedSearchResults.Length, searchResults.Count - expectedSearchResults.Length);

            string error = string.Format("Improper search results for {0}", searchText);
            if (AnyOrder)
            {
                foreach (string expectedSearchResult in expectedSearchResults)
                {
                    Assert.IsTrue(searchResults.Any((SearchItem item) => { return item.Word.Hanzi == expectedSearchResult; }), error);
                }
            }
            else
            {
                for (int i = 0; i < expectedSearchResults.Length; ++i)
                {
                    Assert.AreEqual(expectedSearchResults[i], searchResults[i].Hanzi, error);
                }
            }
        }

        private void RunSearchTest(string searchText, params string[] searchResults)
        {
            RunSearchTest(searchText, false, searchResults);
        }

        /// <summary>
        /// Test the search quality.
        /// Given a search query, assert that the specified words comes in the top results
        /// </summary>
        [TestMethod]
        public void TestSearchQuality()
        {
            RunSearchTest("eat", true, "吃", "哺", "啖", "用饭", "食");
            RunSearchTest("中国人", "中国人", "中", "中国", "国", "国人", "人");
            RunSearchTest("zhongguo", "中国");
            RunSearchTest("shanghai", true, "上海", "伤害");
            RunSearchTest("shanghai airport", true, "浦东机场", "虹桥机场");
        }
    }
}

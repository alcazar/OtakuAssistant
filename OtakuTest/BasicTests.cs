using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OtakuLib;

namespace OtakuTest
{
    [TestClass]
    public class BasicTests
    {
        public void TestStringSearch(string search, string str, int expectedStart, int expectedEnd, SearchFlags searchFlags = SearchFlags.NONE)
        {
            StringSearch.Result expected = new StringSearch.Result(expectedStart, expectedEnd);

            StringSearch stringSearch = new StringSearch(search, searchFlags);
            StringSearch.Result result1 = stringSearch.SearchIn(str, searchFlags);
            StringSearch.Result result2 = StringSearch.Search(search, str, searchFlags);

            if (expected.Found)
            {
                Assert.AreEqual(expected, result1);
                Assert.AreEqual(expected, result2);
            }
            else
            {
                Assert.IsFalse(result1.Found);
                Assert.IsFalse(result2.Found);
            }
        }

        [TestMethod]
        public void TestStringSearch()
        {
            TestStringSearch("abcabcdef", "abcabcdef", 0, 9);
            TestStringSearch("abcabcdef", "abcABCdef", 0, 0);
            TestStringSearch("abcABCdef", "abcabcdef", 0, 0);
            TestStringSearch("abcabcdef", "abcABCdef", 0, 9, SearchFlags.IGNORE_CASE);
            TestStringSearch("abcABCdef", "abcabcdef", 0, 9, SearchFlags.IGNORE_CASE);
            
            TestStringSearch("abcabcdef", "abcabcabcdef", 3, 12);
            TestStringSearch("abcabcdef", "abcabcabcdefdef", 3, 12);
            TestStringSearch("abcabcdef", "abcabcadbcdef", 0, 0);

            TestStringSearch("shanghai", "Pudong Airport (Shanghai)", 16, 24, SearchFlags.IGNORE_CASE);
            TestStringSearch("shanghai", "shàng hǎi", 0, 9, SearchFlags.IGNORE_DIACRITICS | SearchFlags.IGNORE_NON_LETTER);
            TestStringSearch("shàng hǎi", "shanghai", 0, 8, SearchFlags.IGNORE_DIACRITICS | SearchFlags.IGNORE_NON_LETTER);
        }
    }
}

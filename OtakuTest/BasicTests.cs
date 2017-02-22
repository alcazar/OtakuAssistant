using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OtakuLib;

namespace OtakuTest
{
    [TestClass]
    public class BasicTests
    {
        [TestMethod]
        public void TestStringSearch()
        {
            StringSearch search = new StringSearch("abcabcdef");

            Assert.AreEqual(new StringSearch.Result(0, 9), search.SearchIn("abcabcdef"));
            Assert.AreEqual(new StringSearch.Result(3, 12), search.SearchIn("abcabcabcdef"));
            Assert.AreEqual(new StringSearch.Result(3, 12), search.SearchIn("abcabcabcdefdef"));
            Assert.IsTrue(search.SearchIn("abcabcabcdef").Found);
            Assert.IsFalse(search.SearchIn("abcabcadbcdef").Found);

            search = new StringSearch("shanghai");
            
            Assert.AreEqual(new StringSearch.Result(16, 24), search.SearchIn("Pudong Airport (Shanghai)"));
            Assert.AreEqual(new StringSearch.Result(0, 9), search.SearchIn("shàng hǎi", SearchFlags.IGNORE_DIACRITICS | SearchFlags.IGNORE_NON_LETTER));

            search = new StringSearch("airport");

            Assert.AreEqual(new StringSearch.Result(7, 14), search.SearchIn("Pudong Airport (Shanghai)"));
        }
    }
}

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OtakuLib;

namespace DictionaryBinarizer
{
    class Program
    {
        static void Main(string[] args)
        {
            Directory.SetCurrentDirectory("../../../OtakuAssistant");

            DotNetFS fs = new DotNetFS();

            new XmlDictionaryLoader("Cedict_CN_ENG", fs, false);

            WordDictionary.Loading.AsyncWaitHandle.WaitOne();

            BinDictionaryWriter.WriteDictionary("Cedict_CN_ENG", fs).Wait();
        }
    }
}

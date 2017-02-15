using System;
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
            DotNetFS fs = new DotNetFS();

            new XmlDictionaryLoader("Cedict_CN_ENG", fs, false);

            DictionaryLoader.Current.LoadTask.Wait();

            BinDictionaryWriter.WriteDictionary("Cedict_CN_ENG", DictionaryLoader.Current.LoadTask.Result, fs).Wait();
        }
    }
}

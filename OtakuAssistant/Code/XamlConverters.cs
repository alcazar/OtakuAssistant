using System;
using System.ComponentModel;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

using OtakuLib;

namespace OtakuAssistant
{
    public class SearchViewNameSize : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string str = value as string;
            if (str == null)
            {
                return 40;
            }
            else if (str.Length <= 3)
            {
                return 30;
            }
            else if (str.Length <= 4)
            {
                return 24;
            }
            else if (str.Length <= 5)
            {
                return 20;
            }
            else if (str.Length <= 10)
            {
                return 18;
            }
            else
            {
                return 14;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return null;
        }
    }
    
    public class SearchViewNameWrapper : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string str = value as string;
            if (str.Length <= 5)
            {
                return str;
            }
            else if (str.Length <= 10)
            {
                int sep = (str.Length + 1)/2;
                return string.Format("{0}\n{1}", str.Substring(0, sep), str.Substring(sep));
            }
            else
            {
                int sep = (str.Length + 2)/3;
                return string.Format("{0}\n{1}\n{2}", str.Substring(0, sep), str.Substring(sep, sep), str.Substring(2*sep));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return null;
        }
    }
    
    public class WordViewPinyinLine : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            Str[] pinyins = value as Str[];
            if (pinyins != null)
            {
                return string.Join(", ", pinyins);
            }
            else
            {
                return "Placeholder, Pinyin";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return null;
        }
    }
}

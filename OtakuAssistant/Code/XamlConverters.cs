﻿using System;
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
    public class SearchViewHanziSize : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string str = value as string;
            if (str == null)
            {
                return 0.0;
            }
            else if (str.Length <= 3)
            {
                return 30.0;
            }
            else if (str.Length <= 4)
            {
                return 24.0;
            }
            else if (str.Length <= 5)
            {
                return 20.0;
            }
            else
            {
                return 14.0;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return null;
        }
    }
    
    public class SearchViewHanziWrapper : IValueConverter
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

    public class WordViewHanziSize : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            StringPointer str = (StringPointer)value;
            if (str.Length <= 6)
            {
                return 48.0;
            }
            else if (str.Length <= 12)
            {
                return 40.0;
            }
            else if (str.Length <= 20)
            {
                return 32.0;
            }
            else
            {
                return 24.0;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return null;
        }
    }
    
    public class WordViewHanziWrapper : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            StringPointer str = (StringPointer)value;
            if (str.Length <= 5)
            {
                return str;
            }
            else if (str.Length <= 16)
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
            StringList? pinyins = value as StringList?;
            if (pinyins != null)
            {
                return string.Join(", ", pinyins.Value);
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

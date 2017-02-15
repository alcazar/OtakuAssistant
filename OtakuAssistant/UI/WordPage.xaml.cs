using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using OtakuLib;

namespace OtakuAssistant
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WordPage : Page
    {
        public Word PageWord;

        public WordPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            PageWord = e.Parameter as Word;

            DataContext = PageWord;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoForward)
            {
                Frame.GoForward();
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(WordSearchPage), PageWord.Hanzi);
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            // not yet implemented
        }
    }
}

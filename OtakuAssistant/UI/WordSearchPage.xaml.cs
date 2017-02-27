using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.System.Threading;
using Windows.ApplicationModel.Core;
using OtakuLib;

namespace OtakuAssistant
{
    public sealed partial class WordSearchPage : Page
    {
        private SearchResult LastResults = null;

        public WordSearchPage()
        {
            this.InitializeComponent();
        }

        private void ClearSearch()
        {
            WordListView.ItemsSource = new SearchResult();
        }
        
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SearchBox.Text = (e.Parameter as string) ?? string.Empty;
            ClearSearch();
            Search();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.NavigationMode != NavigationMode.Refresh)
            {
                if (e.NavigationMode != NavigationMode.Back)
                {
                    Frame.BackStack.RemoveAt(Frame.BackStack.Count - 1);
                    Frame.BackStack.Add(new PageStackEntry(GetType(), SearchBox.Text, null));
                }
            }
        }

        private void Search()
        {
            string searchText = SearchBox.Text;

            if (searchText == string.Empty)
            {
                ClearSearch();
            }
            else
            {
                WordSearch search = new WordSearch(searchText, UpdateSearchResults);
            }
        }

        private void UpdateSearchResults(WordSearch search)
        {
            LastResults = search.Results;

            IAsyncAction action = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, UpdateSearchResultsUI);
        }

        private void UpdateSearchResultsUI()
        {
            WordListView.ItemsSource = LastResults;
        }
        
        private void searchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Search();
        }

        private void wordListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            (Window.Current.Content as Frame).Navigate(typeof(WordPage), ((SearchItem)e.ClickedItem).Word);
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
    }
}

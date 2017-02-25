using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace OtakuAssistant
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WordSearchPage : Page
    {
        private WordSearch CurrentSearch = null;
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
                StopSearch();
                if (e.NavigationMode != NavigationMode.Back)
                {
                    (Window.Current.Content as Frame).BackStack.RemoveAt((Window.Current.Content as Frame).BackStack.Count - 1);
                    (Window.Current.Content as Frame).BackStack.Add(new PageStackEntry(GetType(), SearchBox.Text, null));
                }
            }
        }

        private void StopSearch()
        {
            if (CurrentSearch != null)
            {
                CurrentSearch.SearchTaskCanceller.Cancel();
            }
        }

        private void Search()
        {
            string searchText = SearchBox.Text;

            StopSearch();

            if (searchText == string.Empty)
            {
                ClearSearch();
            }
            else
            {
                CurrentSearch = new WordSearch(searchText, CurrentSearch);
                CurrentSearch.SearchTask.ContinueWith(UpdateSearchResults);
            }
        }

        private void UpdateSearchResults(Task<SearchResult> task)
        {
            if (!task.IsCanceled)
            {
                // backup the search results in case we throw a new search before UI update
                LastResults = task.Result;
                // we can safely mark there is no more search ongoing
                CurrentSearch = null;

                IAsyncAction action = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, UpdateSearchResultsUI);
            }
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

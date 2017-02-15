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

        public WordSearchPage()
        {
            this.InitializeComponent();
        }

        private void ClearChildren()
        {
            WordListView.ItemsSource = new SearchResult();
        }
        
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SearchBox.Text = (e.Parameter as string) ?? string.Empty;
            ClearChildren();
            Search();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.NavigationMode != NavigationMode.Back)
            {
                if (e.NavigationMode != NavigationMode.Refresh)
                {
                    (Window.Current.Content as Frame).BackStack.RemoveAt((Window.Current.Content as Frame).BackStack.Count - 1);
                    (Window.Current.Content as Frame).BackStack.Add(new PageStackEntry(GetType(), SearchBox.Text, null));
                }
            }
        }

        private void Search()
        {
            string searchText = SearchBox.Text;

            if (searchText != string.Empty)
            {
                if (CurrentSearch != null)
                {
                    CurrentSearch.SearchTaskCanceller.Cancel();
                }

                CurrentSearch = new WordSearch(searchText);
                CurrentSearch.SearchTask.ContinueWith(UpdateSearchResults);
            }
        }

        private void UpdateSearchResults(Task<SearchResult> task)
        {
            IAsyncAction action = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, UpdateSearchResultsUI);
        }

        private void UpdateSearchResultsUI()
        {
            WordListView.ItemsSource = CurrentSearch.SearchTask.Result;
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

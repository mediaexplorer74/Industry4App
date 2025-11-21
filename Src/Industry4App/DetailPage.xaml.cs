using HtmlAgilityPack;
using System;
using System.Linq;
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Industry4App
{
    public sealed partial class DetailPage : Page
    {
        public DetailPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // ------------
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility
                = AppViewBackButtonVisibility.Visible;

            SystemNavigationManager.GetForCurrentView().BackRequested += (s, a) =>
            {
                // If we don't have proper parameters, go back
                if (Frame.CanGoBack)
                    Frame.GoBack();
                a.Handled = true;
            };

            if (ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1, 0))
            {
                Windows.Phone.UI.Input.HardwareButtons.BackPressed += (s, a) =>
                {
                    // If we don't have proper parameters, go back
                    if (Frame.CanGoBack)
                        Frame.GoBack();
                    a.Handled = true;
                };
            }
            // ------------   

            if (e.Parameter is NewsItem item)
            {
                HeaderTitle.Text = item.Title;
                
                // Если содержимое статьи уже загружено, отображаем его
                if (!string.IsNullOrEmpty(item.ArticleContent))
                {
                    ArticleContentTextBlock.Text = item.ArticleContent;
                    
                    // Показываем изображение, если оно есть
                    if (!string.IsNullOrEmpty(item.ImageUrl) && item.ImageUrl != "ms-appx:///Assets/StoreLogo.png")
                    {
                        ArticleImage.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri(item.ImageUrl));
                    }
                    
                    // Показываем заголовок и обзор
                    ArticleTitleTextBlock.Text = item.Title;
                    ArticleOverviewTextBlock.Text = item.Summary;
                }
                else if (!string.IsNullOrEmpty(item.ArticleUrl))
                {
                    // Если содержимое не загружено, но URL доступен, загружаем содержимое
                    LoadArticleContent(item.ArticleUrl);
                }
            }
        }

        private async void LoadArticleContent(string articleUrl)
        {
            try
            {
                using (Windows.Web.Http.HttpClient client = new Windows.Web.Http.HttpClient())
                {
                    // Притворяемся обычным браузером, чтобы нас не заблокировали
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0.4430.93 Safari/537.36");

                    string html = await client.GetStringAsync(new Uri(articleUrl));

                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    // Ищем заголовок статьи
                    var titleNode = doc.DocumentNode.Descendants("h1")
                        .FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("article__header__title-in"));
                    
                    // Ищем основное изображение статьи
                    var imageNode = doc.DocumentNode.Descendants("img")
                        .FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("article__main-image__image"));
                    
                    // Ищем обзор (краткое содержание)
                    var overviewNode = doc.DocumentNode.Descendants("div")
                        .FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("article__text__overview"));
                    
                    // Ищем основной текст статьи
                    var contentNode = doc.DocumentNode.Descendants("div")
                        .FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("article__text"));

                    string title = titleNode?.InnerText?.Trim() ?? "";
                    string imageUrl = imageNode?.GetAttributeValue("src", "") ?? "";
                    string overview = overviewNode?.InnerText?.Trim() ?? "";
                    string content = "";

                    if (contentNode != null)
                    {
                        // Извлекаем только текст из основного содержимого, игнорируя вложенные элементы типа рекламы
                        var mainContent = contentNode.Descendants("div")
                            .FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("article__text__main"));
                        
                        if (mainContent != null)
                        {
                            // Удаляем inline-элементы (рекламные блоки)
                            var inlineItems = mainContent.Descendants("div")
                                .Where(n => n.GetAttributeValue("class", "").Contains("article__inline-item"))
                                .ToList();
                            
                            foreach (var inlineItem in inlineItems)
                            {
                                inlineItem.Remove();
                            }
                            
                            // Удаляем специальные контейнеры (подписка, реклама), оставляя только полезные (например, об авторе)
                            var specialContainers = mainContent.Descendants("div")
                                .Where(n => n.GetAttributeValue("class", "").Contains("article__special_container"))
                                .ToList();
                            
                            foreach (var container in specialContainers)
                            {
                                // Проверяем, содержит ли контейнер информацию об эксперте
                                var innerText = container.InnerText.ToLower();
                                if (!innerText.Contains("об эксперте") && !innerText.Contains("эксперт"))
                                {
                                    // Удаляем контейнер, если он не содержит информацию об эксперте
                                    container.Remove();
                                }
                            }
                            
                            content = mainContent.InnerText.Trim();
                            
                            // Заменяем &nbsp; на обычный пробел
                            content = content.Replace("&nbsp;", " ");
                        }
                        else
                        {
                            content = contentNode.InnerText.Trim();
                            
                            // Заменяем &nbsp; на обычный пробел
                            content = content.Replace("&nbsp;", " ");
                        }
                    }

                    // Обновляем UI в основном потоке
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        // Обновляем изображение статьи
                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            ArticleImage.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri(imageUrl));
                        }
                        
                        // Обновляем заголовок статьи
                        if (!string.IsNullOrEmpty(title))
                        {
                            // Заменяем &nbsp; на обычный пробел в заголовке
                            ArticleTitleTextBlock.Text = title.Replace("&nbsp;", " ");
                        }
                        
                        // Обновляем обзор статьи
                        if (!string.IsNullOrEmpty(overview))
                        {
                            // Заменяем &nbsp; на обычный пробел в обзоре
                            ArticleOverviewTextBlock.Text = overview.Replace("&nbsp;", " ");
                        }
                        
                        // Обновляем основной контент статьи
                        if (!string.IsNullOrEmpty(content))
                        {
                            ArticleContentTextBlock.Text = content;
                        }
                        else
                        {
                            ArticleContentTextBlock.Text = "Не удалось загрузить содержимое статьи.";
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    ArticleContentTextBlock.Text = $"Ошибка при загрузке содержимого: {ex.Message}";
                });
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}
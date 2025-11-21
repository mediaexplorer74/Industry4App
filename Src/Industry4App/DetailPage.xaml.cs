using HtmlAgilityPack;
using System;
using System.Linq;
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using System.Collections.Generic;
using System.Threading.Tasks;

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
                // Формируем заголовок страницы в формате "Summary - Title" с ограничением длины заголовка
                string title = item.Title ?? "";
                if (title.Length > 20)
                {
                    title = title.Substring(0, 20) + "...";
                }
                HeaderTitle.Text = $"{item.Summary} - {title}";
                
                // Если содержимое статьи уже загружено, отображаем его
                if (!string.IsNullOrEmpty(item.ArticleContent))
                {
                    DisplayArticleContent(item.ArticleContent);
                    
                    // Показываем изображение, если оно есть
                    if (!string.IsNullOrEmpty(item.ImageUrl) && item.ImageUrl != "ms-appx:///Assets/assets/default_icon.png")
                    {
                        ArticleImage.Source = new BitmapImage(new Uri(item.ImageUrl));
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
                            
                            content = mainContent.InnerHtml ?? mainContent.InnerText ?? "";
                        }
                        else
                        {
                            content = contentNode.InnerHtml ?? contentNode.InnerText ?? "";
                        }
                    }

                    // Обновляем UI в основном потоке
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        // Обновляем изображение статьи
                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            ArticleImage.Source = new BitmapImage(new Uri(imageUrl));
                        }
                        
                        // Обновляем заголовок статьи
                        if (!string.IsNullOrEmpty(title))
                        {
                            // Заменяем &nbsp; на обычный пробел, заменяем &laquo; и &raquo; на кавычки, заменяем &mdash; на дефис в заголовке
                            ArticleTitleTextBlock.Text = title.Replace("&nbsp;", " ")
                                                             .Replace("&laquo;", "\"")
                                                             .Replace("&raquo;", "\"")
                                                             .Replace("&mdash;", "-")
                                                             .Replace("&ndash;", "-"); // Также заменяем короткое тире
                        }
                        
                        // Обновляем обзор статьи
                        if (!string.IsNullOrEmpty(overview))
                        {
                            // Заменяем &nbsp; на обычный пробел, &laquo; и &raquo; на кавычки, &mdash; на дефис в обзоре
                            ArticleOverviewTextBlock.Text = overview.Replace("&nbsp;", " ")
                                                                   .Replace("&laquo;", "\"")
                                                                   .Replace("&raquo;", "\"")
                                                                   .Replace("&mdash;", "-")
                                                                   .Replace("&ndash;", "-"); // Также заменяем короткое тире
                        }
                        
                        // Обновляем основной контент статьи
                        if (!string.IsNullOrEmpty(content))
                        {
                            DisplayArticleContent(content);
                        }
                        else
                        {
                            DisplayArticleContent("<p>Не удалось загрузить содержимое статьи.</p>");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    DisplayArticleContent($"<p>Ошибка при загрузке содержимого: {ex.Message}</p>");
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
        
        /// <summary>
        /// Отображает содержимое статьи с поддержкой изображений
        /// </summary>
        /// <param name="htmlContent">HTML содержимое статьи</param>
        private void DisplayArticleContent(string htmlContent)
        {
            // Очищаем панель содержимого, оставляя только заголовок, изображение и обзор
            ArticleContentPanel.Children.Clear();
            
            // Добавляем обратно заголовок, изображение и обзор
            ArticleContentPanel.Children.Add(ArticleImage);
            ArticleContentPanel.Children.Add(ArticleTitleTextBlock);
            ArticleContentPanel.Children.Add(ArticleOverviewTextBlock);
            
            // Парсим HTML содержимое
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);
            
            // Проходим по всем элементам и добавляем их в панель
            foreach (var node in doc.DocumentNode.ChildNodes)
            {
                if (node.Name == "p" || node.Name == "div")
                {
                    // Для текстовых элементов создаем TextBlock
                    var textBlock = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    
                    // Обрабатываем внутреннее содержимое, сохраняя форматирование
                    textBlock.Text = ProcessHtmlText(node.InnerHtml ?? node.InnerText ?? "");
                    
                    ArticleContentPanel.Children.Add(textBlock);
                }
                else if (node.Name == "img")
                {
                    // Для изображений создаем Image элемент
                    var imageUrl = node.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        try
                        {
                            var image = new Image
                            {
                                Stretch = Windows.UI.Xaml.Media.Stretch.Uniform,
                                MaxHeight = 400,
                                Margin = new Thickness(0, 10, 0, 10)
                            };
                            
                            image.Source = new BitmapImage(new Uri(imageUrl));
                            ArticleContentPanel.Children.Add(image);
                        }
                        catch
                        {
                            // Игнорируем ошибки загрузки изображений
                        }
                    }
                }
                else if (node.Name == "#text" && !string.IsNullOrWhiteSpace(node.InnerText))
                {
                    // Для текстовых узлов создаем TextBlock
                    var textBlock = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    
                    textBlock.Text = ProcessHtmlText(node.InnerText);
                    
                    if (!string.IsNullOrWhiteSpace(textBlock.Text))
                    {
                        ArticleContentPanel.Children.Add(textBlock);
                    }
                }
            }
        }
        
        /// <summary>
        /// Обрабатывает HTML текст, заменяя специальные символы
        /// </summary>
        /// <param name="text">Текст для обработки</param>
        /// <returns>Обработанный текст</returns>
        private string ProcessHtmlText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // Заменяем HTML сущности
            text = text.Replace("&nbsp;", " ")
                       .Replace("&laquo;", "\"")
                       .Replace("&raquo;", "\"")
                       .Replace("&mdash;", "-")
                       .Replace("&ndash;", "-")
                       .Replace("&quot;", "\"")
                       .Replace("&lt;", "<")
                       .Replace("&gt;", ">")
                       .Replace("&amp;", "&");
            
            // Удаляем HTML теги
            text = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", string.Empty);
            
            // Нормализуем пробелы
            text = NormalizeText(text);
            
            return text;
        }
        
        /// <summary>
        /// Удаляет лишние пустые строки и нормализует пробелы в тексте, сохраняя структуру абзацев
        /// </summary>
        /// <param name="text">Входной текст</param>
        /// <returns>Нормализованный текст</returns>
        private string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // Заменяем множественные пробелы на одиночные, но сохраняем переводы строк
            string normalized = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
            
            // Заменяем множественные переводы строк на максимум 2
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\r?\n[ \t]*\r?\n[ \t]*", "\n\n");
            
            // Удаляем пробелы в начале и конце строк
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"^[ \t]+", "", 
                System.Text.RegularExpressions.RegexOptions.Multiline);
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[ \t]+$", "", 
                System.Text.RegularExpressions.RegexOptions.Multiline);
            
            return normalized.Trim();
        }
    }
}
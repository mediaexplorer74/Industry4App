using HtmlAgilityPack;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Industry4App
{
    public sealed partial class MainPage : Page
    {
        // Коллекция, которая автоматически обновляет интерфейс при добавлении элементов
        public ObservableCollection<NewsItem> NewsItems { get; set; } = new ObservableCollection<NewsItem>();

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (NewsItems.Count == 0)
            {
                await LoadNews();
            }
            
            // Включаем возможность обновления через pull-to-refresh
            if (RefreshContainer != null)
            {
                RefreshContainer.RefreshRequested += async (refreshTrigger, args) =>
                {
                    // Показываем индикатор загрузки
                    var deferral = args.GetDeferral();
                    
                    // Загружаем новости с обновлением
                    await LoadNews(true);
                    
                    // Скрываем индикатор загрузки
                    deferral.Complete();
                };
            }
        }

        private async Task LoadNews(bool isRefresh = false)
        {
            string url = "https://trends.rbc.ru/trends/industry";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Притворяемся обычным браузером, чтобы нас не заблокировали
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0.4430.93 Safari/537.36");

                    string html = await client.GetStringAsync(url);

                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    // ВАЖНО: Здесь мы ищем карточки новостей.
                    // На момент написания RBC использует разные классы, попробуем найти общие контейнеры.
                    // Обычно карточки лежат в div с классами js-library-card или item__wrap

                    var articles = doc.DocumentNode.Descendants("article")
                        .Where(node => node.GetAttributeValue("class", "").Contains("item js-load-item"))
                        .Take(50); // Берем до 50 штук

                    // Если это обновление (pull-to-refresh), очищаем старые элементы
                    if (isRefresh)
                    {
                        NewsItems.Clear();
                    }

                    foreach (var article in articles)
                    {
                        var item = new NewsItem();

                        // 1. Ищем заголовок статьи
                        // Структура заголовка на сайте RBC: <span class="item__title"><a class="g-inline-text-badges js-item-link" ...><span class="g-inline-text-badges__text">Заголовок статьи</span></a></span>
                        var titleContainer = article.Descendants("span")
                            .FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("item__title"));
                        
                        if (titleContainer != null)
                        {
                            var linkNode = titleContainer.Descendants("a")
                                .FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("js-item-link"));
                            
                            if (linkNode != null)
                            {
                                item.ArticleUrl = linkNode.GetAttributeValue("href", "");
                                // Если ссылка относительная (начинается с /), добавляем домен
                                if (!item.ArticleUrl.StartsWith("http"))
                                {
                                    item.ArticleUrl = "https://trends.rbc.ru" + item.ArticleUrl;
                                }

                                // Находим заголовок статьи внутри span с классом g-inline-text-badges__text
                                var titleNode = linkNode.Descendants("span")
                                    .FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("g-inline-text-badges__text"));
                                    
                                item.Title = titleNode?.InnerText?.Trim() ?? "";
                                
                                // Заменяем символы &nbsp;, &laquo;, &raquo;, &mdash; в заголовке
                                item.Title = item.Title.Replace("&nbsp;", " ")
                                                       .Replace("&laquo;", "\"")
                                                       .Replace("&raquo;", "\"")
                                                       .Replace("&mdash;", "-")
                                                       .Replace("&ndash;", "-");
                            }
                        }

                        // Если заголовок не найден, пробуем альтернативные способы
                        if (string.IsNullOrEmpty(item.Title))
                        {
                            // Пробуем найти заголовок напрямую в article
                            var titleNode = article.Descendants("span")
                                .FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("title") ||
                                                   n.GetAttributeValue("class", "").Contains("item__title"));
                            item.Title = titleNode?.InnerText?.Trim() ?? "";
                        }

                        // 2. Ищем картинку
                        var imgNode = article.Descendants("img").FirstOrDefault();
                        if (imgNode != null)
                        {
                            item.ImageUrl = imgNode.GetAttributeValue("src", "");
                            // Иногда картинки лежат в data-src для ленивой загрузки
                            if (string.IsNullOrEmpty(item.ImageUrl))
                            {
                                item.ImageUrl = imgNode.GetAttributeValue("data-src", "");
                            }
                        }
                        
                        // Если картинка не найдена в img, пробуем найти в background-image стиле
                        if (string.IsNullOrEmpty(item.ImageUrl))
                        {
                            var bgImgNode = article.Descendants()
                                .FirstOrDefault(n => n.GetAttributeValue("style", "").Contains("background-image"));
                            if (bgImgNode != null)
                            {
                                var style = bgImgNode.GetAttributeValue("style", "");
                                var startIndex = style.IndexOf("background-image:");
                                if (startIndex != -1)
                                {
                                    startIndex = style.IndexOf("url(", startIndex);
                                    if (startIndex != -1)
                                    {
                                        startIndex += 4; // длина "url("
                                        var endIndex = style.IndexOf(")", startIndex);
                                        if (endIndex != -1)
                                        {
                                            item.ImageUrl = style.Substring(startIndex, endIndex - startIndex)
                                                .Trim(new char[] { '"', '\'' });
                                        }
                                    }
                                }
                            }
                        }

                        // Заглушка, если картинки нет
                        if (string.IsNullOrEmpty(item.ImageUrl))
                        {
                            item.ImageUrl = "ms-appx:///Assets/StoreLogo.png";
                        }

                        // 3. Ищем краткое описание (Subtitle)
                        // На сайте RBC в карточках новостей краткое описание часто отсутствует,
                        // поэтому будем использовать категорию статьи как дополнительную информацию
                        var categoryNode = article.Descendants("div")
                            .FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("item__category"));
                        
                        if (categoryNode != null)
                        {
                            var categoryLink = categoryNode.Descendants("a").FirstOrDefault();
                            item.Summary = categoryLink?.InnerText?.Trim() ?? "Нет категории";
                        }
                        else
                        {
                            item.Summary = "Нет описания";
                        }

                        /*if (!string.IsNullOrEmpty(item.Summary))
                        {
                            item.Title = item.Summary;
                        }*/

                        // Добавляем в список, если нашли хотя бы заголовок
                        if (!string.IsNullOrEmpty(item.Title))
                        {
                            // Проверяем, что элемент с таким URL еще не добавлен
                            if (!NewsItems.Any(i => i.ArticleUrl == item.ArticleUrl))
                            {
                                // Заменяем символы &nbsp;, &laquo;, &raquo;, &mdash; в кратком описании
                                item.Summary = item.Summary.Replace("&nbsp;", " ")
                                                          .Replace("&laquo;", "\"")
                                                          .Replace("&raquo;", "\"")
                                                          .Replace("&mdash;", "-")
                                                          .Replace("&ndash;", "-");
                                
                                NewsItems.Add(item);
                            }
                        }
                    }
                    
                    // После загрузки всех элементов, если это обновление, прокручиваем список к началу
                    if (isRefresh)
                    {
                        // Используем NewsItems, так как это имя коллекции, привязанной к ListView
                        if (NewsItems.Count > 0)
                        {
                            // Прокручиваем к первому элементу
                            var firstItem = NewsItems.FirstOrDefault();
                            if (firstItem != null)
                            {
                                // Находим ListView в интерфейсе по имени
                                var listView = this.FindName("NewsListView") as ListView;
                                if (listView != null)
                                {
                                    listView.ScrollIntoView(firstItem);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Простая обработка ошибок
                var dialog = new Windows.UI.Popups.MessageDialog("Ошибка загрузки: " + ex.Message);
                await dialog.ShowAsync();
            }
        }

        // Обработка клика по плитке
        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedItem = e.ClickedItem as NewsItem;
            if (clickedItem != null)
            {
                Frame.Navigate(typeof(DetailPage), clickedItem);
            }
        }
    }
}
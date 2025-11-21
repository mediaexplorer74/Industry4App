using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Industry4App
{
    // Это то, как выглядит наша "новость"
    public class NewsItem
    {
        public string Title { get; set; }       // Заголовок
        public string ImageUrl { get; set; }    // Ссылка на картинку
        public string ArticleUrl { get; set; }  // Ссылка на саму статью
        public string Summary { get; set; }     // Краткое описание (если есть)
    }
}

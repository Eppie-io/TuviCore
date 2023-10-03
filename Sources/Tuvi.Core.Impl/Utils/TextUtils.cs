using MimeKit.Text;

namespace Tuvi.Core.Utils
{
    public class TextUtils : ITextUtils
    {
        public string GetTextFromHtml(string html)
        {
            var previewer = new HtmlTextPreviewer { MaximumPreviewLength = 1024 }; // TODO: TVM-281 necessary to make it possible to display all HTML text, even if it is larger than 1024 bytes
            var result = previewer.GetPreviewText(html);

            if (string.IsNullOrWhiteSpace(result))
            {
                result = previewer.GetPreviewText("<html><body>" + html + "</body></html>");
            }

            return string.IsNullOrWhiteSpace(result) ? html : result;
        }
    }
}

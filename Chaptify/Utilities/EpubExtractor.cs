using HtmlAgilityPack;
using System.Text;
using System.Text.RegularExpressions;
using VersOne.Epub;

namespace Chaptify.Utilities;

public static partial class EpubExtractor
{
    public static void Extract(string epubPath, string outputDir, Action<string, double> onProgress)
    {
        using EpubBookRef book = EpubReader.OpenBook(epubPath);

        if (!Directory.Exists(outputDir))
        {
            _ = Directory.CreateDirectory(outputDir);
        }

        Dictionary<string, string> navigationMap = BuildNavigationMap(book);

        List<EpubLocalTextContentFileRef> readingOrder = book.GetReadingOrder();
        int total = readingOrder.Count;
        int current = 0;
        int padWidth = Math.Max(3, total.ToString().Length);

        for (int i = 0; i < total; i++)
        {
            EpubLocalTextContentFileRef item = readingOrder[i];
            double progressPercent = (double)(i + 1) / total * 100;

            string filePathKey = GetFilePathWithoutAnchor(item.FilePath);
            string title = GetTitle(item, filePathKey, navigationMap) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(title))
            {
                onProgress?.Invoke("Skipped (No Title)", progressPercent);
                continue;
            }

            string content = item.ReadContent();
            string plainText = ConvertHtmlToText(content);

            if (plainText.Length < 50)
            {
                onProgress?.Invoke($"Skipped: {title}", progressPercent);
                continue;
            }

            current++;

            string safeTitle = MakeSafeFileName(title);
            string fileName = $"{current.ToString().PadLeft(padWidth, '0')} - {safeTitle}.txt";
            string outputPath = Path.Combine(outputDir, fileName);

            File.WriteAllText(outputPath, plainText, Encoding.UTF8);
            onProgress?.Invoke(title, progressPercent);
        }
    }

    public static string GetFilePathWithoutAnchor(string filePath)
    {
        int anchorIndex = filePath.IndexOf('#');
        return anchorIndex >= 0 ? filePath[..anchorIndex] : filePath;
    }

    private static Dictionary<string, string> BuildNavigationMap(EpubBookRef book)
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
        List<EpubNavigationItemRef>? navigation = book.GetNavigation();
        if (navigation == null)
        {
            return map;
        }

        void Visit(IEnumerable<EpubNavigationItemRef> items)
        {
            foreach (EpubNavigationItemRef item in items)
            {
                if (item.Link != null && !string.IsNullOrEmpty(item.Link.ContentFilePath))
                {
                    string key = GetFilePathWithoutAnchor(item.Link.ContentFilePath);
                    if (!map.ContainsKey(key) && !string.IsNullOrWhiteSpace(item.Title))
                    {
                        map[key] = item.Title.Trim();
                    }
                }

                Visit(item.NestedItems);
            }
        }

        Visit(navigation);
        return map;
    }

    private static string? GetTitle(EpubLocalTextContentFileRef item, string filePathKey,
        Dictionary<string, string> navMap)
    {
        if (navMap.TryGetValue(filePathKey, out string? navTitle) && !string.IsNullOrWhiteSpace(navTitle))
        {
            if (!IsGenericTitle(navTitle))
            {
                return navTitle;
            }
        }

        try
        {
            string content = item.ReadContent();
            HtmlDocument doc = new();
            doc.LoadHtml(content.Length > 3000 ? content[..3000] : content);

            // ReSharper disable once ConstantNullCoalescingCondition - SelectSingleNode CAN return null
            HtmlNode body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;

            if (body.SelectSingleNode(".//h1") is { } h1)
            {
                string h1Text = NormalizeWhitespace().Replace(HtmlEntity.DeEntitize(h1.InnerText).Trim(), " ");
                if (!string.IsNullOrWhiteSpace(h1Text) && !IsGenericTitle(h1Text))
                {
                    return h1Text;
                }
            }

            if (body.SelectSingleNode(".//h2") is { } h2)
            {
                string h2Text = NormalizeWhitespace().Replace(HtmlEntity.DeEntitize(h2.InnerText).Trim(), " ");
                if (!string.IsNullOrWhiteSpace(h2Text) && !IsGenericTitle(h2Text))
                {
                    return h2Text;
                }
            }

            if (body.SelectSingleNode(".//h3") is { } h3)
            {
                string h3Text = NormalizeWhitespace().Replace(HtmlEntity.DeEntitize(h3.InnerText).Trim(), " ");
                if (!string.IsNullOrWhiteSpace(h3Text) && !IsGenericTitle(h3Text))
                {
                    return h3Text;
                }
            }
        }
        catch (Exception)
        {
            // Silently ignore HTML parsing errors - return null to fallback
        }

        return null;
    }

    public static bool IsGenericTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return true;
        }

        if (int.TryParse(title, out _))
        {
            return true;
        }

        if (title.Length <= 2)
        {
            return true;
        }

        string lower = title.ToLowerInvariant();
        return lower is "toc" or "table of contents" or "mục lục" or "cover" or "bìa" or "title page" or "copyright";
    }

    public static string MakeSafeFileName(string title)
    {
        string safe = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
        safe = NormalizeWhitespace().Replace(safe, " ").Trim();
        if (safe.Length > 100)
        {
            safe = safe[..100].Trim();
        }

        return safe;
    }

    public static string ConvertHtmlToText(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        HtmlDocument doc = new();
        doc.LoadHtml(html);

        HtmlNode root = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;

        List<HtmlNode> junkNodes = root.Descendants()
            .Where(n => n.Name is "script" or "style" or "head" or "nav" ||
                        IsHiddenNode(n))
            .ToList();

        foreach (HtmlNode node in junkNodes)
        {
            node.Remove();
        }

        StringBuilder sb = new();
        ProcessNode(root, sb);

        string result = sb.ToString();

        List<string> lines = result.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        CleanUpHeaderLines(lines);

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private static void ProcessNode(HtmlNode node, StringBuilder sb)
    {
        switch (node.NodeType)
        {
            case HtmlNodeType.Text:
                string text = HtmlEntity.DeEntitize(node.InnerText);
                text = NormalizeWhitespace().Replace(text, " ");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _ = sb.Append(text);
                }

                break;

            case HtmlNodeType.Element:
                bool isBlock = IsBlockElement(node.Name);

                if (isBlock && sb.Length > 0 && !sb.ToString().EndsWith('\n'))
                {
                    _ = sb.Append('\n');
                }

                if (node.Name == "li")
                {
                    _ = sb.Append("- ");
                }

                if (node.HasChildNodes)
                {
                    foreach (HtmlNode child in node.ChildNodes)
                    {
                        ProcessNode(child, sb);
                    }
                }

                if (isBlock)
                {
                    _ = sb.Append('\n');
                }

                if (node.Name == "br")
                {
                    _ = sb.Append('\n');
                }

                break;
        }
    }

    private static bool IsBlockElement(string tagName)
    {
        return tagName is "p" or "div" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
            or "li" or "ul" or "ol" or "blockquote" or "article" or "section" or "pre";
    }

    private static void CleanUpHeaderLines(List<string> lines)
    {
        while (lines.Count > 0 && int.TryParse(lines[0], out _))
        {
            lines.RemoveAt(0);
        }

        if (lines.Count <= 1)
        {
            return;
        }

        string firstLine = lines[0].Normalize(NormalizationForm.FormC);
        int duplicateIndex = -1;

        for (int i = 1; i < Math.Min(10, lines.Count); i++)
        {
            string currentLine = lines[i].Normalize(NormalizationForm.FormC);
            if (string.Equals(firstLine, currentLine, StringComparison.OrdinalIgnoreCase))
            {
                duplicateIndex = i;
                break;
            }

            if (firstLine.Length > 5 && currentLine.StartsWith(firstLine, StringComparison.OrdinalIgnoreCase))
            {
                duplicateIndex = i;
                break;
            }
        }

        if (duplicateIndex > 0)
        {
            lines.RemoveRange(1, duplicateIndex);
        }
    }

    private static bool IsHiddenNode(HtmlNode n)
    {
        string style = n.GetAttributeValue("style", string.Empty);
        string cssClass = n.GetAttributeValue("class", string.Empty);

        return style.Contains("display:none", StringComparison.OrdinalIgnoreCase) ||
               style.Contains("visibility:hidden", StringComparison.OrdinalIgnoreCase) ||
               cssClass.Contains("hidden", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex NormalizeWhitespace();
}
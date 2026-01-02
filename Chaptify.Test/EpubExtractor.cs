using Chaptify.Utilities;

namespace Chaptify.Test;

public class EpubExtractorTests
{
    [Theory]
    [InlineData("chapter1.xhtml#section1", "chapter1.xhtml")]
    [InlineData("chapter1.xhtml#", "chapter1.xhtml")]
    [InlineData("chapter1.xhtml", "chapter1.xhtml")]
    [InlineData("path/to/chapter.xhtml#anchor", "path/to/chapter.xhtml")]
    [InlineData("#onlyanchor", "")]
    public void GetFilePathWithoutAnchor_RemovesAnchorCorrectly(string input, string expected)
    {
        string result = EpubExtractor.GetFilePathWithoutAnchor(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("123")]
    [InlineData("ab")]
    [InlineData("toc")]
    [InlineData("TOC")]
    [InlineData("Table of Contents")]
    [InlineData("Mục lục")]
    [InlineData("cover")]
    [InlineData("Cover")]
    [InlineData("Bìa")]
    [InlineData("Title Page")]
    [InlineData("Copyright")]
    public void IsGenericTitle_ReturnsTrue_ForGenericTitles(string title)
    {
        Assert.True(EpubExtractor.IsGenericTitle(title));
    }

    [Theory]
    [InlineData("Chapter 1: Introduction")]
    [InlineData("Chương 1: Hắn chính là một viên tro bụi")]
    [InlineData("Prologue")]
    [InlineData("Epilogue")]
    [InlineData("Part One")]
    public void IsGenericTitle_ReturnsFalse_ForValidTitles(string title)
    {
        Assert.False(EpubExtractor.IsGenericTitle(title));
    }

    [Theory]
    [InlineData("Chapter 1: Test", "Chapter 1_ Test")]
    [InlineData("Hello/World", "Hello_World")]
    [InlineData("Test<>File", "Test__File")]
    [InlineData("Normal Title", "Normal Title")]
    [InlineData("  Spaces  Around  ", "Spaces Around")]
    public void MakeSafeFileName_RemovesInvalidChars(string input, string expected)
    {
        string result = EpubExtractor.MakeSafeFileName(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MakeSafeFileName_TruncatesLongNames()
    {
        string longTitle = new('A', 150);
        string result = EpubExtractor.MakeSafeFileName(longTitle);
        Assert.True(result.Length <= 100);
    }

    [Fact]
    public void MakeSafeFileName_HandlesEmptyString()
    {
        string result = EpubExtractor.MakeSafeFileName("");
        Assert.Equal("", result);
    }

    [Fact]
    public void ConvertHtmlToText_ReturnsEmpty_ForEmptyInput()
    {
        string result = EpubExtractor.ConvertHtmlToText("");
        Assert.Equal("", result);
    }

    [Fact]
    public void ConvertHtmlToText_ExtractsTextFromBody()
    {
        string html = @"
<!DOCTYPE html>
<html>
<head><title>0</title></head>
<body>
<p>Hello World</p>
</body>
</html>";

        string result = EpubExtractor.ConvertHtmlToText(html);
        Assert.Contains("Hello World", result);
        Assert.DoesNotContain("<p>", result);
        Assert.DoesNotContain("<title>", result);
    }

    [Fact]
    public void ConvertHtmlToText_RemovesScriptAndStyle()
    {
        string html = @"
<body>
<script>alert('test');</script>
<style>.hidden { display: none; }</style>
<p>Visible content</p>
</body>";

        string result = EpubExtractor.ConvertHtmlToText(html);
        Assert.Contains("Visible content", result);
        Assert.DoesNotContain("alert", result);
        Assert.DoesNotContain(".hidden", result);
    }

    [Fact]
    public void ConvertHtmlToText_RemovesHiddenElements()
    {
        string html = @"
<body>
<div style=""display:none"">Hidden text</div>
<p>Visible text</p>
</body>";

        string result = EpubExtractor.ConvertHtmlToText(html);
        Assert.Contains("Visible text", result);
    }

    [Fact]
    public void ConvertHtmlToText_HandlesVietnameseCharacters()
    {
        string html = @"
<body>
<p>Chương 1: Hắn chính là một viên tro bụi</p>
<p>Đọc truyện và tham gia tu tiên</p>
</body>";

        string result = EpubExtractor.ConvertHtmlToText(html);
        Assert.Contains("Chương 1", result);
        Assert.Contains("Hắn chính là một viên tro bụi", result);
        Assert.Contains("Đọc truyện", result);
    }

    [Fact]
    public void ConvertHtmlToText_DecodesHtmlEntities()
    {
        string html = @"<body><p>Test &amp; Demo &lt;html&gt;</p></body>";

        string result = EpubExtractor.ConvertHtmlToText(html);
        Assert.Contains("Test & Demo <html>", result);
    }

    [Fact]
    public void ConvertHtmlToText_RemovesLeadingNumericLines()
    {
        string html = @"
<body>
<p>0</p>
<p>123</p>
<p>Actual content here</p>
</body>";

        string result = EpubExtractor.ConvertHtmlToText(html);
        Assert.Contains("Actual content here", result);
    }

    [Fact]
    public void ConvertHtmlToText_RemovesDuplicateTitles()
    {
        string html = @"
<body>
<h1>Chapter Title</h1>
<p>Some junk text</p>
<p>Chapter Title</p>
<p>Actual content</p>
</body>";

        string result = EpubExtractor.ConvertHtmlToText(html);
        Assert.Contains("Chapter Title", result);
        Assert.Contains("Actual content", result);
    }

    [Fact]
    public void ConvertHtmlToText_HandlesList()
    {
        string html = @"
<body>
<ul>
<li>Item 1</li>
<li>Item 2</li>
</ul>
</body>";

        string result = EpubExtractor.ConvertHtmlToText(html);
        Assert.Contains("- Item 1", result);
        Assert.Contains("- Item 2", result);
    }

    [Fact]
    public void ConvertHtmlToText_HandlesLineBreaks()
    {
        string html = @"<body><p>Line 1<br/>Line 2</p></body>";

        string result = EpubExtractor.ConvertHtmlToText(html);
        Assert.Contains("Line 1", result);
        Assert.Contains("Line 2", result);
    }
}

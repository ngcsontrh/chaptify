using Chaptify.Utilities;
using Spectre.Console;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

AnsiConsole.Write(
    new FigletText("Chaptify")
        .LeftJustified()
        .Color(Color.Teal));

while (true)
{
    AnsiConsole.MarkupLine("Vui lòng chọn chức năng:");
    AnsiConsole.MarkupLine("1. Trích xuất EPUB");
    AnsiConsole.MarkupLine("2. Thoát");

    string input = AnsiConsole.Ask<string>("[green]Vui lòng nhập lựa chọn (1-2): [/]");

    if (input is not "1" and not "2")
    {
        AnsiConsole.MarkupLine("[red]Lựa chọn không hợp lệ. Vui lòng thử lại![/]");
        AnsiConsole.WriteLine();
        continue;
    }

    if (input == "2")
    {
        break;
    }

    string filePath = AnsiConsole.Ask<string>("Nhập đường dẫn đến file [yellow]EPUB[/]:");

    filePath = filePath.Trim('"');

    if (!File.Exists(filePath))
    {
        AnsiConsole.MarkupLine("[red]Không tìm thấy file![/]");
        continue;
    }

    try
    {
        FileInfo fileInfo = new(filePath);
        string directory = fileInfo.DirectoryName ?? ".";
        string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
        string outputDir = Path.Combine(directory, fileNameNoExt);

        AnsiConsole.MarkupLine(string.Format("Đã tạo thư mục đầu ra: [blue]{0}[/]", outputDir));

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                ProgressTask task = ctx.AddTask("Đang xử lý các chương...");

                await Task.Run(() =>
                {
                    EpubExtractor.Extract(filePath, outputDir, (title, percent) =>
                    {
                        task.Value = percent;
                        task.Description = string.Format("Đang trích xuất: {0}", title);
                    });
                });

                task.Value = 100;
            });

        AnsiConsole.MarkupLine("[bold green]Trích xuất hoàn tất thành công![/]");
        AnsiConsole.MarkupLine("[gray]--------------------------------------------------[/]");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine(string.Format("[red]Có lỗi xảy ra: {0}[/]", ex.Message));
    }
}

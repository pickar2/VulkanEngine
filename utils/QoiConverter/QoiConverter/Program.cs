using System.Diagnostics;
using QoiSharp;
using QoiSharp.Codec;
using Spectre.Console;
using StbImageSharp;

const string outputFileExtension = "qoi";
string inFolder = Path.Combine(Environment.CurrentDirectory, "in");
string outFolder = Path.Combine(Environment.CurrentDirectory, "out");

if (!Directory.Exists(inFolder))
{
	Directory.CreateDirectory(inFolder);
	Process.Start("explorer.exe", inFolder);
	AnsiConsole.WriteLine("Fill \"in\" folder with images and press any key to start conversion.");
	Console.ReadKey();
}

string[] files = Directory.GetFiles(inFolder, "*", SearchOption.AllDirectories);
if (files.Length == 0)
{
	AnsiConsole.MarkupLine($"[red]Files not found in \"{inFolder}\\\"![/]\nPress any key to exit...");
	Console.ReadKey();
	return;
}
AnsiConsole.MarkupLine("[yellow]Next files will be converted:[/]");
// ReSharper disable once ConvertClosureToMethodGroup
Array.ForEach(files, filePath => AnsiConsole.WriteLine(Beautify(filePath)));
if (!Directory.Exists(outFolder)) Directory.CreateDirectory(outFolder);
if (!AnsiConsole.Confirm("[yellow]Start conversion?[/]")) return;

await AnsiConsole.Progress().StartAsync(async ctx =>
{
	var visualTask = ctx.AddTask("[white]Conversion[/]");
	var tasks = new Task[files.Length];
	double incrementValueForTask = 100.0 / files.Length;
	for (int index = 0; index < files.Length; index++)
		tasks[index] = Task.Factory.StartNew(fileName => ConversionTask((string) fileName), files[index]);

	foreach (var task in tasks)
	{
		await task;
		visualTask.Increment(incrementValueForTask);
	}
});

AnsiConsole.MarkupLine("[green]Success![/]\nPress any key to exit...");
Console.ReadKey();

void ConversionTask(string file)
{
	try
	{
		using var fileStream = File.OpenRead(file);
		var image = ImageResult.FromStream(fileStream);
		File.WriteAllBytes(Path.Combine(outFolder, $"{Path.GetFileNameWithoutExtension(file)}.{outputFileExtension}"),
			image.SourceComp switch
			{
				ColorComponents.Grey or ColorComponents.GreyAlpha => ExtendAndToQoi(image),
				ColorComponents.RedGreenBlue or ColorComponents.RedGreenBlueAlpha => ToQoi(image.Data, image.Width,
					image.Height, (int)image.SourceComp),
				_ => throw new NotSupportedException("Try to lose data!")
			});
	}
	catch (Exception ex)
	{
		AnsiConsole.WriteException(ex);
	}
}

string Beautify(string path)
{
	return $".{path.Replace(inFolder, "")}";
}

byte[] ExtendAndToQoi(ImageResult image)
{
	int toChannels = image.SourceComp switch
	{
		ColorComponents.Grey => 3,
		ColorComponents.GreyAlpha => 4,
		_ => throw new NotSupportedException($"Can't extend {image.SourceComp} to RGB or RGBA!")
	};
	
	byte[] newData = new byte[image.Width * image.Height * toChannels];
	for (int i = 0; i < image.Width * image.Height; i++)
	for (int rgbaIndex = 0; rgbaIndex < toChannels; rgbaIndex++)
		newData[(i * toChannels) + rgbaIndex] = image.Data[i * (int)image.SourceComp];

	return ToQoi(newData, image.Width, image.Height, toChannels);
}

byte[] ToQoi(ReadOnlySpan<byte> data, int width, int height, int numChannels)
{
	var channels = numChannels == 4 ? Channels.RgbWithAlpha : Channels.Rgb;
	return QoiEncoder.Encode(new QoiImage(
		data.ToArray(),
		width,
		height,
		channels));
}

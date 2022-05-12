using System;
using System.IO;
using System.Text;
using Core.Registries.Entities;

namespace Core.Logs.Entities.Handlers;

internal sealed class FileLogHandler : ILogHandler
{
	private const LogLevels DefaultValue = LogLevels.Fatal | LogLevels.Error;
	private const string FileNameDateFormat = "dd-MM-yyyy";
	private const string Prefix = "log_";
	private const int PrefixLength = 4;
	private const string Extension = ".txt";
	private const int MaxArchiveFilesCount = 5;
	private readonly string _filePath;
	private readonly NamespacedName _namespacedName = NamespacedName.CreateWithCoreNamespace("file-log-levels");

	internal FileLogHandler()
	{
		string fileName = string.Concat(Prefix, DateOnly.FromDateTime(DateTime.Now).ToString(FileNameDateFormat), Extension);
		_filePath = Path.Combine(App.LogsPath, fileName);
		if (File.Exists(_filePath)) return;

		string[] files = Directory.GetFiles(App.LogsPath, $"*{Extension}", SearchOption.TopDirectoryOnly);
		int count = files.Length;
		if (count <= MaxArchiveFilesCount) return;

		Span<ArchiveData> dates = stackalloc ArchiveData[count];
		for (int index = 0; index < count; index++)
			dates[index] = new ArchiveData
			{
				Date = DateOnly.ParseExact(Path.GetFileNameWithoutExtension(files[index])[PrefixLength..], FileNameDateFormat, null),
				IsExists = true
			};

		do
		{
			int minIndex = -1;
			for (int index = 0; index < files.Length; index++)
				if (minIndex == -1 && dates[index].IsExists)
					minIndex = index;
				else if (dates[index].IsExists && dates[minIndex].Date > dates[index].Date)
					minIndex = index;

			dates[minIndex].IsExists = false;
			File.Delete(files[minIndex]);
			count--;
		} while (count > MaxArchiveFilesCount);
	}

	LogLevels ILogHandler.Levels { get; set; } = DefaultValue;

	void ILogHandler.Write<T>(T message, string? suffix)
	{
		string markupMessage = $"[{DateTime.Now.ToString(LoggerRegistry.LogDateFormat)}|{suffix}] {message?.ToString() ?? string.Empty}\n";
		using var writer = new FileStream(_filePath, FileMode.Append);
		int sizeOfString = Encoding.UTF8.GetByteCount(markupMessage);
		Span<byte> span = stackalloc byte[sizeOfString];
		Encoding.UTF8.GetBytes(markupMessage, span).ThrowIfNotEquals(sizeOfString);
		writer.Write(span);
		writer.Close();
	}

	LogLevels ILogHandler.UpdateState() => ((ILogHandler) this).Levels = ConfigRegistry.DeveloperStates.GetOrRegister(_namespacedName, DefaultValue);

	private struct ArchiveData
	{
		public DateOnly Date;
		public bool IsExists;
	}
}

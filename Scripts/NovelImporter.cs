// NovelImporter.cs
// This script handles reading the JSONL file in a separate task/thread
// and safely creates/updates UI elements on the main thread.

using Godot;
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

public partial class NovelImporter : Node
{
	[Signal]
	public delegate void ImportStartedEventHandler();
	[Signal]
	public delegate void NovelDataParsedEventHandler(NovelData novelDataResource);
	[Signal]
	public delegate void ImportProgressEventHandler(int currentLine);
	[Signal]
	public delegate void ImportFinishedEventHandler(int totalImported, int totalErrors);
	[Signal]
	public delegate void ImportFailedEventHandler(string errorMessage);

	private string _filePath = "";
	private bool _isImporting = false;
	private Task _importTask = null;

	[Export]
	public string CoversFolderPath { get; set; } = "user://novelpia_covers/";

	public bool IsImporting()
	{
		return _isImporting;
	}

	public void ImportNovels(string jsonlFilePath)
	{
		if (_isImporting)
		{
			GD.PushWarning("Import already in progress.");
			return;
		}

		_filePath = jsonlFilePath;
		if (!Godot.FileAccess.FileExists(_filePath))
		{
			EmitSignal(SignalName.ImportFailed, $"File not found: {_filePath}");
			return;
		}

		_isImporting = true;
		EmitSignal(SignalName.ImportStarted);
		_importTask = Task.Run(() => _ThreadImportTask(_filePath));
	}

	private void _ThreadImportTask(string path)
	{
		Godot.FileAccess file = null;
		try
		{
			file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
			if (file == null)
			{
				CallDeferred(MethodName.EmitImportFailed, $"Could not open file: {path}");
				return;
			}

			int importedCount = 0;
			int errorCount = 0;
			long currentLineNum = 0;
			
			while (!file.EofReached())
			{
				string line = file.GetLine();
				currentLineNum++;
				if (string.IsNullOrWhiteSpace(line)) continue;

				NovelData novelData = null;
				try
				{
					var jsonDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);

					string id = jsonDict.TryGetValue("id", out var idElem) ? idElem.GetString() : "";
					string title = jsonDict.TryGetValue("title", out var titleElem) ? titleElem.GetString() : "";
					string synopsis = jsonDict.TryGetValue("synopsis", out var synopsisElem) ? synopsisElem.GetString() : "";
					string author = jsonDict.TryGetValue("author", out var authorElem) ? authorElem.GetString() : "";
					
					List<string> tagsList = new List<string>();
					if (jsonDict.TryGetValue("tags", out var tagsElem) && tagsElem.ValueKind == JsonValueKind.Array)
					{
						foreach (var tag in tagsElem.EnumerateArray())
						{
							tagsList.Add(tag.GetString());
						}
					}
					string[] tags = tagsList.ToArray();

					bool isAdult = jsonDict.TryGetValue("is_adult", out var isAdultElem) && isAdultElem.GetBoolean();
					string publicationStatus = jsonDict.TryGetValue("publication_status", out var pubStatusElem) ? pubStatusElem.GetString() : "";
					string coverUrl = jsonDict.TryGetValue("cover_url", out var coverUrlElem) ? coverUrlElem.GetString() : "";
					
					int likeCount = 0;
					if (jsonDict.TryGetValue("like_count", out var likeCountElem) && likeCountElem.ValueKind == JsonValueKind.Number)
					{
						likeCount = likeCountElem.GetInt32();
					}
					
					int chapterCount = 0;
					if (jsonDict.TryGetValue("chapter_count", out var chapterCountElem) && chapterCountElem.ValueKind == JsonValueKind.Number)
					{
						chapterCount = chapterCountElem.GetInt32();
					}

					novelData = new NovelData(
						id, title, synopsis, author, tags, isAdult, publicationStatus,
						coverUrl, "", likeCount, chapterCount
					);

					string expectedCoverFileName = $"{novelData.Id}.jpg";
					novelData.CoverLocalPath = System.IO.Path.Combine(CoversFolderPath, expectedCoverFileName).Replace("\\", "/");
				}
				catch (Exception e)
				{
					GD.PushError($"Error processing line {currentLineNum}: {line.Trim()} - {e.Message}");
					errorCount++;
					continue;
				}

				if (novelData != null)
				{
					CallDeferred(MethodName.EmitNovelDataParsed, novelData);
					importedCount++;
				}

				if (currentLineNum % 100 == 0)
				{
					CallDeferred(MethodName.EmitImportProgress, (int)currentLineNum);
				}
			}
			
			CallDeferred(MethodName.EmitImportProgress, (int)currentLineNum);
			CallDeferred(MethodName.EmitImportFinished, importedCount, errorCount);
		}
		catch (Exception e)
		{
			CallDeferred(MethodName.EmitImportFailed, $"An error occurred during import: {e.Message}");
		}
		finally
		{
			file?.Dispose();
			_isImporting = false;
			_importTask = null;
		}
	}

	private void EmitNovelDataParsed(NovelData novelDataResource) => EmitSignal(SignalName.NovelDataParsed, novelDataResource);
	private void EmitImportProgress(int currentLine) => EmitSignal(SignalName.ImportProgress, currentLine);
	private void EmitImportFinished(int totalImported, int totalErrors) => EmitSignal(SignalName.ImportFinished, totalImported, totalErrors);
	private void EmitImportFailed(string errorMessage) => EmitSignal(SignalName.ImportFailed, errorMessage);
}

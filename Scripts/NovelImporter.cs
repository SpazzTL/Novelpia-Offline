// NovelImporter.cs
// This script handles reading the JSONL file in a separate task/thread
// and safely creates/updates UI elements on the main thread.

using Godot;
using System;
using System.IO; // For Path.GetExtension
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks; // For Task.Run
using System.Linq; // Required for .Contains() on arrays/IEnumerable

public partial class NovelImporter : Node
{
	// Signals (Godot Events) to communicate with the main thread
	[Signal]
	public delegate void ImportStartedEventHandler();
	[Signal]
	public delegate void NovelDataParsedEventHandler(NovelData novelDataResource); // Emits one NovelData resource at a time
	[Signal]
	public delegate void ImportProgressEventHandler(int currentLine); // Modified: Removed totalLines
	[Signal]
	public delegate void ImportFinishedEventHandler(int totalImported, int totalErrors);
	[Signal]
	public delegate void ImportFailedEventHandler(string errorMessage);

	private string _filePath = "";
	private bool _isImporting = false;
	private Task _importTask = null;

	// Path to the folder where covers are stored relative to res:// or user://
	[Export]
	public string CoversFolderPath { get; set; } = "res://novelpia_covers/";

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
		// Explicitly use Godot.FileAccess
		if (!Godot.FileAccess.FileExists(_filePath))
		{
			EmitSignal(SignalName.ImportFailed, $"File not found: {_filePath}");
			return;
		}

		_isImporting = true;
		EmitSignal(SignalName.ImportStarted);

		// Start the import task in a separate thread
		_importTask = Task.Run(() => _ThreadImportTask(_filePath));
		// You might want to add error handling for the task itself here,
		// e.g., using .ContinueWith() to catch unhandled exceptions in the task.
	}

	// Changed from async Task to void as it's not using await internally
	private void _ThreadImportTask(string path)
	{
		// This function runs in a separate thread.
		// File I/O and JSON parsing are thread-safe.
		Godot.FileAccess file = null; // Explicitly use Godot.FileAccess
		try
		{
			file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
			if (file == null)
			{
				// Corrected CallDeferred usage
				CallDeferred(MethodName.EmitImportFailed, $"Could not open file: {path}");
				return;
			}

			// Removed the first pass to count totalLines for extreme performance
			// totalLines will now be unknown until the end of the import.
			// Progress reporting will be based on current line number only.

			int importedCount = 0;
			int errorCount = 0;
			long currentLineNum = 0;

			// Define allowed image extensions once
			string[] allowedImageExtensions = {".png", ".jpg", ".jpeg", ".gif", ".webp"};

			// Changed to invoke EofReached() as a method
			bool eofReachedForProcess = file.EofReached(); 
			while (eofReachedForProcess == false)
			{
				string line = file.GetLine();
				currentLineNum++;
				if (string.IsNullOrWhiteSpace(line))
				{
					eofReachedForProcess = file.EofReached(); // Update temp variable
					continue;
				}

				NovelData novelData = null;
				try
				{
					// Deserialize JSON string into a Dictionary first to handle potential missing fields gracefully
					var jsonDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);

					// Extract values, handling potential nulls or missing keys
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
					string coverLocalPath = jsonDict.TryGetValue("cover_local_path", out var coverLocalPathElem) ? coverLocalPathElem.GetString() : "";
					
					int likeCount = 0; // Default to 0
					if (jsonDict.TryGetValue("like_count", out var likeCountElem) && likeCountElem.ValueKind == JsonValueKind.Number)
					{
						likeCount = likeCountElem.GetInt32();
					}
					
					int chapterCount = 0; // Default to 0
					if (jsonDict.TryGetValue("chapter_count", out var chapterCountElem) && chapterCountElem.ValueKind == JsonValueKind.Number)
					{
						chapterCount = chapterCountElem.GetInt32();
					}

					novelData = new NovelData(
						id, title, synopsis, author, tags, isAdult, publicationStatus,
						coverUrl, coverLocalPath, likeCount, chapterCount
					);

					// If cover_local_path was null in the JSON, construct it based on ID and CoversFolderPath
					// This is crucial if your scraper only provides the URL and you need to infer the local path.
					// Adjust this logic if your scraper *always* provides the correct local path.
					if (string.IsNullOrEmpty(novelData.CoverLocalPath) && !string.IsNullOrEmpty(novelData.CoverUrl))
					{
						string fileExtension = Path.GetExtension(novelData.CoverUrl);
						// Use System.Linq.Contains() for array check
						if (string.IsNullOrEmpty(fileExtension) || !allowedImageExtensions.Contains(fileExtension.ToLower()))
						{
							fileExtension = ".png"; // Default to .png if no valid extension or .file
						}
						// Ensure the path is relative to Godot's resource system if it's in res://
						novelData.CoverLocalPath = Path.Combine(CoversFolderPath, $"{novelData.Id}{fileExtension}");
						// Normalize path for Godot
						novelData.CoverLocalPath = novelData.CoverLocalPath.Replace("\\", "/");
					}


				}
				catch (JsonException e)
				{
					GD.PushError($"Failed to parse JSON on line {currentLineNum}: {line.Trim()} - {e.Message}");
					errorCount++;
					eofReachedForProcess = file.EofReached(); // Update temp variable
					continue;
				}
				catch (Exception e)
				{
					GD.PushError($"Unexpected error processing line {currentLineNum}: {line.Trim()} - {e.Message}");
					errorCount++;
					eofReachedForProcess = file.EofReached(); // Update temp variable
					continue;
				}

				if (novelData != null)
				{
					// Emit signal to main thread about parsed data (using CallDeferred for thread safety)
					CallDeferred(MethodName.EmitNovelDataParsed, novelData); // Corrected: Defer helper method
					importedCount++;
				}

				// Emit progress periodically to avoid overwhelming the main thread with signals
				if (currentLineNum % 100 == 0) // Update every 100 lines
				{
					CallDeferred(MethodName.EmitImportProgress, (int)currentLineNum); // Modified: Removed totalLines
				}
				eofReachedForProcess = file.EofReached(); // Update temp variable for next iteration
			}
			
			// Emit final progress and finished signal
			CallDeferred(MethodName.EmitImportProgress, (int)currentLineNum); // Modified: Removed totalLines
			CallDeferred(MethodName.EmitImportFinished, importedCount, errorCount); // Corrected CallDeferred usage
		}
		catch (Exception e)
		{
			CallDeferred(MethodName.EmitImportFailed, $"An error occurred during import: {e.Message}"); // Corrected CallDeferred usage
		}
		finally
		{
			file?.Dispose(); // Ensure the file is closed
			_isImporting = false;
			_importTask = null;
		}
	}

	// --- Helper methods for deferred signal emission ---
	private void EmitNovelDataParsed(NovelData novelDataResource)
	{
		EmitSignal(SignalName.NovelDataParsed, novelDataResource);
	}

	private void EmitImportProgress(int currentLine) // Modified: Removed totalLines
	{
		EmitSignal(SignalName.ImportProgress, currentLine);
	}

	private void EmitImportFinished(int totalImported, int totalErrors)
	{
		EmitSignal(SignalName.ImportFinished, totalImported, totalErrors);
	}

	private void EmitImportFailed(string errorMessage)
	{
		EmitSignal(SignalName.ImportFailed, errorMessage);
	}
}

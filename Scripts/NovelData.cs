// NovelData.cs
// This script defines a custom Godot Resource to hold novel metadata.
// Resources are excellent for data containers that can be instantiated, saved, and managed.

using Godot;
using System;
using System.Collections.Generic;

public partial class NovelData : Resource
{
	[Export] // Makes the property visible and editable in the Godot editor
	public string Id { get; set; } = "";

	[Export]
	public string Title { get; set; } = "";

	[Export]
	public string Synopsis { get; set; } = "";

	[Export]
	public string Author { get; set; } = "";

	[Export]
	public string[] Tags { get; set; } = new string[0]; // Use string array for tags

	[Export]
	public bool IsAdult { get; set; } = false;

	[Export]
	public string PublicationStatus { get; set; } = ""; // e.g., "연재중", "완결", "연재중단"

	[Export]
	public string CoverUrl { get; set; } = ""; // Original URL for the cover image

	[Export]
	public string CoverLocalPath { get; set; } = ""; // Local path to the downloaded cover image

	[Export]
	public int LikeCount { get; set; } = 0; // Changed from int? to int, default to 0

	[Export]
	public int ChapterCount { get; set; } = 0; // Changed from int? to int, default to 0

	// Default constructor (required for Godot to instantiate resources)
	public NovelData() : base() { }

	// Parameterized constructor for easy initialization
	public NovelData(
		string id,
		string title,
		string synopsis,
		string author,
		string[] tags,
		bool isAdult,
		string publicationStatus,
		string coverUrl,
		string coverLocalPath,
		int likeCount, // Changed from int? to int
		int chapterCount) : base() // Changed from int? to int
	{
		Id = id;
		Title = title;
		Synopsis = synopsis;
		Author = author;
		Tags = tags;
		IsAdult = isAdult;
		PublicationStatus = publicationStatus;
		CoverUrl = coverUrl;
		CoverLocalPath = coverLocalPath;
		LikeCount = likeCount;
		ChapterCount = chapterCount;
	}

	// Optional: Override ToString for easier debugging
	public override string ToString()
	{
		return $"[NovelData] ID: {Id}, Title: {Title}, Author: {Author}";
	}
}

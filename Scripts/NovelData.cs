// NovelData.cs
// This script defines a custom Godot Resource to hold novel metadata.
// Resources are excellent for data containers that can be instantiated, saved, and managed.

using Godot;
using Godot.Collections; // Required for Godot's collection types

public partial class NovelData : Resource
{
	[Export]
	public string Id { get; set; } = "";

	[Export]
	public string Title { get; set; } = "";

	[Export]
	public string Synopsis { get; set; } = "";

	[Export]
	public string Author { get; set; } = "";

	[Export]
	public string[] Tags { get; set; } = new string[0];

	// CORRECTED: Changed from List<string> to Godot's Array<string> for engine compatibility.
	[Export]
	public Array<string> Categories { get; set; } = new Array<string>();

	[Export]
	public bool IsAdult { get; set; } = false;

	[Export]
	public string PublicationStatus { get; set; } = "";

	[Export]
	public string CoverUrl { get; set; } = "";

	[Export]
	public string CoverLocalPath { get; set; } = "";

	[Export]
	public int LikeCount { get; set; } = 0;

	[Export]
	public int ChapterCount { get; set; } = 0;

	public NovelData() : base() { }

	// Constructor updated to work with the corrected properties.
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
		int likeCount,
		int chapterCount) : base()
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
		Categories = new Array<string>(); // Initialize as a new Godot Array
	}

	public override string ToString()
	{
		return $"[NovelData] ID: {Id}, Title: {Title}, Author: {Author}";
	}
}

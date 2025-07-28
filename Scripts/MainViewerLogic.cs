// MainViewerLogic.cs
using Godot;
using System;
using System.Linq; // For .Any() and .Contains() on arrays
using System.Collections.Generic; // Explicitly added for Dictionary and List
using System.IO; // For Path.GetExtension
using System.Text.Json;
using Godot.Collections;


public partial class MainViewerLogic : Control
//region Variables
{
	[Export] // Drag your NovelImporter node here in the editor
	public NovelImporter NovelImporterNode { get; set; }

	[Export] // Drag your StatusLabel here
	public Label StatusLabel { get; set; }

	[Export] // Drag your ProgressBar here
	public ProgressBar ProgressBar { get; set; }

	[Export] // Drag your NovelListContainer (GridContainer) here
	public GridContainer NovelListContainer { get; set; } 

	// UI element for general text search
	[Export]
	public LineEdit SearchLineEdit { get; set; }

	// Pagination UI elements
	[Export]
	public Button PrevPageButton { get; set; }
	[Export]
	public Button NextPageButton { get; set; }
	[Export]
	public Label PageNumberLabel { get; set; }

	// UI elements for direct page navigation
	[Export]
	public LineEdit GoToPageLineEdit { get; set; }
	[Export]
	public Button GoToPageButton { get; set; }

	// New UI elements for filtering
	[Export]
	public LineEdit TagFilterLineEdit { get; set; } // Retained for text-based tag search
	[Export]
	public LineEdit AuthorFilterLineEdit { get; set; }
	[Export]
	public OptionButton StatusFilterOptionButton { get; set; }
	[Export]
	public LineEdit MinChaptersLineEdit { get; set; }
	[Export]
	public LineEdit MaxChaptersLineEdit { get; set; }
	[Export]
	public Button ApplyFiltersButton { get; set; } // Explicit button to apply filters

	// New UI elements for additional filters and sorting
	[Export]
	public OptionButton AdultContentFilterOptionButton { get; set; } 
	[Export]
	public OptionButton CoverStatusFilterOptionButton { get; set; } 
	[Export]
	public LineEdit MinLikesLineEdit { get; set; }
	[Export]
	public LineEdit MaxLikesLineEdit { get; set; }
	[Export]
	public OptionButton SortByOptionButton { get; set; }
	[Export]
	public OptionButton SortOrderOptionButton { get; set; }

	// New UI elements for dynamic tag filtering
	[Export]
	public GridContainer TagFilterContainer { get; set; } // Container for dynamic tag checkboxes
	[Export]
	public Button ClearTagFiltersButton { get; set; } // Button to clear selected tags


	// A dictionary to hold all loaded NovelData resources, keyed by ID
	private System.Collections.Generic.Dictionary<string, NovelData> _allNovels = new System.Collections.Generic.Dictionary<string, NovelData>();
	// A list to hold currently filtered novels (for display)
	private List<NovelData> _currentFilteredNovels = new List<NovelData>();
	// List to hold references to dynamically created tag checkboxes
	private List<CheckBox> _tagFilterCheckBoxes = new List<CheckBox>();




	// --- Data Structures for Category and Selection Management ---
	private System.Collections.Generic.Dictionary<string, List<string>> _categories = new System.Collections.Generic.Dictionary<string, List<string>>();
	private HashSet<string> _selectedNovelIds = new HashSet<string>();
	private System.Collections.Generic.Dictionary<string, CheckBox> _novelCheckBoxes = new System.Collections.Generic.Dictionary<string, CheckBox>();
	private const string CategoriesSavePath = "user://novel_categories.json";
	
	// --- State variables for context-aware popup ---
	private bool _isBulkEditingCategories = false;
	private NovelData _currentNovelForCategoryManagement;




	// A placeholder texture to use when cover image loading fails
	private Texture2D _placeholderTexture;

	[Export] // Exported variable for the custom minimum size of each novel entry
	public Vector2 NovelEntryMinSize { get; set; } = new Vector2(500, 500); // Updated to 500x500

	// Pagination parameters
	[Export]
	public int NovelsPerPage { get; set; } = 8; // Number of novels to display per page (e.g., 2 rows of 4)

	private int _currentPage = 1;
	private int _totalPages = 1;
//endregion

	public override void _Ready()
	{
		// Consolidated null checks for all exported UI elements
		if (NovelImporterNode == null || StatusLabel == null || ProgressBar == null || NovelListContainer == null || 
			SearchLineEdit == null || PrevPageButton == null || NextPageButton == null || PageNumberLabel == null ||
			GoToPageLineEdit == null || GoToPageButton == null ||
			TagFilterLineEdit == null || AuthorFilterLineEdit == null || StatusFilterOptionButton == null ||
			MinChaptersLineEdit == null || MaxChaptersLineEdit == null || ApplyFiltersButton == null ||
			AdultContentFilterOptionButton == null || CoverStatusFilterOptionButton == null || MinLikesLineEdit == null ||
			MaxLikesLineEdit == null || SortByOptionButton == null || SortOrderOptionButton == null ||
			TagFilterContainer == null || ClearTagFiltersButton == null) // Added new UI elements
		{
			GD.PushError("One or more UI elements are not assigned in the editor!");
			return;
		}

		// NEW DEBUG PRINT: Confirm NovelsPerPage value
		GD.Print($"[DEBUG] NovelsPerPage set to: {NovelsPerPage}");

		// Initialize placeholder texture (e.g., a simple gray box)
		var image = Image.CreateEmpty(100, 150, false, Image.Format.Rgb8);
		image.Fill(Colors.DarkGray); // A simple dark gray placeholder
		_placeholderTexture = ImageTexture.CreateFromImage(image);

		// Connect to signals from the NovelImporter
		NovelImporterNode.ImportStarted += OnImportStarted;
		NovelImporterNode.NovelDataParsed += OnNovelDataParsed;
		NovelImporterNode.ImportProgress += OnImportProgress;
		NovelImporterNode.ImportFinished += OnImportFinished;
		NovelImporterNode.ImportFailed += OnImportFailed;

		// Connect general search bar text changed signal (this will now trigger ApplyFilters)
		SearchLineEdit.TextChanged += _on_FilterCriteriaChanged; 

		// Connect pagination buttons
		PrevPageButton.Pressed += _on_PrevPageButton_Pressed;
		NextPageButton.Pressed += _on_NextPageButton_Pressed;

		// Connect new Go To Page button
		GoToPageButton.Pressed += _on_GoToPageButton_Pressed;

		// Connect filter UI elements to a common handler
		TagFilterLineEdit.TextChanged += _on_FilterCriteriaChanged;
		AuthorFilterLineEdit.TextChanged += _on_FilterCriteriaChanged;
		MinChaptersLineEdit.TextChanged += _on_FilterCriteriaChanged;
		MaxChaptersLineEdit.TextChanged += _on_FilterCriteriaChanged;
		ApplyFiltersButton.Pressed += _on_ApplyFiltersButtonPressed; // Explicit apply button

		// Connect OptionButtons (Status, Adult Content, Cover Status, Sort By, Sort Order)
		StatusFilterOptionButton.ItemSelected += _on_FilterCriteriaChanged;
		AdultContentFilterOptionButton.ItemSelected += _on_FilterCriteriaChanged; 
		CoverStatusFilterOptionButton.ItemSelected += _on_FilterCriteriaChanged; 
		MinLikesLineEdit.TextChanged += _on_FilterCriteriaChanged;
		MaxLikesLineEdit.TextChanged += _on_FilterCriteriaChanged;
		SortByOptionButton.ItemSelected += _on_FilterCriteriaChanged;
		SortOrderOptionButton.ItemSelected += _on_FilterCriteriaChanged;

		// Connect dynamic tag filter clear button
		ClearTagFiltersButton.Pressed += _on_ClearTagFiltersButtonPressed;


		// Populate StatusFilterOptionButton
		StatusFilterOptionButton.AddItem("All");
		StatusFilterOptionButton.AddItem("연재중"); // Serializing
		StatusFilterOptionButton.AddItem("완결"); // Complete
		StatusFilterOptionButton.AddItem("연재중단"); // Discontinued
		StatusFilterOptionButton.Select(0); // Select "All" by default

		// Populate AdultContentFilterOptionButton
		AdultContentFilterOptionButton.AddItem("Show All");
		AdultContentFilterOptionButton.AddItem("Show Adult Only");
		AdultContentFilterOptionButton.AddItem("Hide Adult");
		AdultContentFilterOptionButton.Select(2); // Default to "Hide Adult"

		// Populate CoverStatusFilterOptionButton
		CoverStatusFilterOptionButton.AddItem("Show All");
		CoverStatusFilterOptionButton.AddItem("Show With Cover Only");
		CoverStatusFilterOptionButton.AddItem("Show Without Cover Only");
		CoverStatusFilterOptionButton.Select(0); // Default to "Show All"

		// Populate SortByOptionButton
		SortByOptionButton.AddItem("Title");
		SortByOptionButton.AddItem("Popularity (Likes)");
		SortByOptionButton.AddItem("Chapter Count");
		SortByOptionButton.AddItem("Relevance"); // For search relevance
		SortByOptionButton.Select(1); // Default sort by Popularity (Likes) - Index 1

		// Populate SortOrderOptionButton
		SortOrderOptionButton.AddItem("Ascending");
		SortOrderOptionButton.AddItem("Descending");
		SortOrderOptionButton.Select(1); // Default sort Descending - Index 1

		// --- Start the import process ---
		var jsonlFilePath = "user://novelpia_metadata.jsonl"; 
		NovelImporterNode.ImportNovels(jsonlFilePath);
	}

	private void OnImportStarted()
	{
		GD.Print("Novel import started...");
		StatusLabel.Text = "Importing novels...";
		ProgressBar.Value = 0;
		ProgressBar.MaxValue = 100;
		ProgressBar.Visible = true;
		ProgressBar.Indeterminate = true;
		
		ClearNovelDisplay();
		_allNovels.Clear();
		_currentFilteredNovels.Clear();
		_tagFilterCheckBoxes.Clear(); // Clear dynamic tag checkboxes
		ClearTagFilterContainer(); // Clear UI for dynamic tags
		
		// Reset pagination
		_currentPage = 1;
		_totalPages = 1;
		UpdatePageNumberLabel();
		UpdatePaginationButtons();
	}






	private void OnNovelDataParsed(NovelData novelDataResource)
	{
		_allNovels[novelDataResource.Id] = novelDataResource;
	}

	private void OnImportProgress(int currentLine)
	{
		StatusLabel.Text = $"Importing: {currentLine} novels processed...";
		ProgressBar.Indeterminate = true;
	}

	private void OnImportFinished(int totalImported, int totalErrors)
	{
		GD.Print("Novel import finished!");
		GD.Print($"Total imported: {totalImported}, Total errors: {totalErrors}");
		StatusLabel.Text = $"Import complete! Total novels: {totalImported}";
		ProgressBar.Value = 100;
		ProgressBar.Indeterminate = false;
		ProgressBar.Visible = false;

		// NEW DEBUG PRINT: Total novels loaded into _allNovels
		GD.Print($"[DEBUG] Total novels loaded into _allNovels: {_allNovels.Count}");

		// Populate dynamic tag filters after all novels are imported
		PopulateTagFilters();

		// Apply initial filters after all novels are imported
		ApplyFilters();
	}

	private void OnImportFailed(string errorMessage)
	{
		GD.PushError($"Novel import failed: {errorMessage}");
		StatusLabel.Text = $"Import failed: {errorMessage}";
		ProgressBar.Value = 0;
		ProgressBar.Indeterminate = false;
		ProgressBar.Visible = false;
	}

	// --- Common Filter Criteria Changed Handler (for various UI elements) ---
	// Overloaded for different signal types
	private void _on_FilterCriteriaChanged(string newText) => ApplyFilters(); // For LineEdits
	private void _on_FilterCriteriaChanged(long index) => ApplyFilters();    // For OptionButtons
	private void _on_FilterCriteriaChanged(bool buttonPressed) => ApplyFilters(); // For dynamic CheckBoxes

	// --- Apply Filters Button Pressed Handler ---
	private void _on_ApplyFiltersButtonPressed()
	{
		ApplyFilters();
	}

	// --- Dynamic Tag Filter Population ---
	private void PopulateTagFilters()
	{
		ClearTagFilterContainer(); // Clear any existing checkboxes
		_tagFilterCheckBoxes.Clear(); // Clear the list of references

		// Collect all tags and count their occurrences
		var tagCounts = new System.Collections.Generic.Dictionary<string, int>();
		foreach (var novel in _allNovels.Values)
		{
			if (novel.Tags != null)
			{
				foreach (var tag in novel.Tags)
				{
					if (!string.IsNullOrEmpty(tag))
					{
						string lowerTag = tag.ToLower();
						tagCounts[lowerTag] = tagCounts.GetValueOrDefault(lowerTag, 0) + 1;
					}
				}
			}
		}

		// Get the top N most frequent tags
		int topTagsCount = 30; // You can adjust this number
		var topTags = tagCounts.OrderByDescending(pair => pair.Value)
								.Take(topTagsCount)
								.Select(pair => pair.Key)
								.ToList();

		// Create a CheckBox for each top tag
		foreach (var tag in topTags)
		{
			var checkBox = new CheckBox();
			checkBox.Text = tag;
			checkBox.Toggled += _on_FilterCriteriaChanged; // Connect to common filter handler
			TagFilterContainer.AddChild(checkBox);
			_tagFilterCheckBoxes.Add(checkBox);
		}

		GD.Print($"Populated {topTags.Count} dynamic tag filters.");
	}

	private void ClearTagFilterContainer()
	{
		foreach (Node child in TagFilterContainer.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void _on_ClearTagFiltersButtonPressed()
	{
		foreach (var checkBox in _tagFilterCheckBoxes)
		{
			checkBox.ButtonPressed = false; // Uncheck all dynamic tag checkboxes
		}
		ApplyFilters(); // Re-apply filters after clearing
		StatusLabel.Text = "Cleared tag filters.";
	}


	// Helper to check if a novel has a valid local cover file
	private bool HasValidCover(NovelData novel)
	{
		return !string.IsNullOrEmpty(novel.CoverLocalPath) &&
			   novel.CoverLocalPath != "SKIPPED_ADULT" &&
			   novel.CoverLocalPath != "SKIPPED_LIMIT" &&
			   Godot.FileAccess.FileExists(novel.CoverLocalPath);
	}

	// --- Core Filtering and Sorting Logic ---
	private void ApplyFilters()
	{
		IEnumerable<NovelData> filtered = _allNovels.Values.AsEnumerable();

	
		// 1. Apply general search text filter (from SearchLineEdit)
		string generalSearchText = SearchLineEdit.Text.Trim().ToLower();
		if (!string.IsNullOrEmpty(generalSearchText))
		{
			filtered = filtered.Where(novel =>
				(novel.Title != null && novel.Title.ToLower().Contains(generalSearchText)) ||
				(novel.Id != null && novel.Id.ToLower().Contains(generalSearchText)) ||
				(novel.Author != null && novel.Author.ToLower().Contains(generalSearchText)) ||
				(novel.Tags != null && novel.Tags.Any(tag => tag.ToLower().Contains(generalSearchText))) ||
				(novel.Synopsis != null && novel.Synopsis.ToLower().Contains(generalSearchText)) 
			);
		}

		// 2. Apply Author filter
		string authorFilterText = AuthorFilterLineEdit.Text.Trim().ToLower();
		if (!string.IsNullOrEmpty(authorFilterText))
		{
			filtered = filtered.Where(novel =>
				novel.Author != null && novel.Author.ToLower().Contains(authorFilterText)
			);
		}

		// 3. Apply Tag filter (from TagFilterLineEdit - comma/semicolon separated, "any" match)
		string tagFilterText = TagFilterLineEdit.Text.Trim();
		if (!string.IsNullOrEmpty(tagFilterText))
		{
			string[] searchTags = tagFilterText.ToLower().Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
												.Select(s => s.Trim()).ToArray();
			
			if (searchTags.Length > 0)
			{
				filtered = filtered.Where(novel =>
					novel.Tags != null && novel.Tags.Any(novelTag =>
						searchTags.Any(searchTag => novelTag.ToLower().Contains(searchTag))
					)
				);
			}
		}

		// 4. Apply Dynamic Tag Checkbox filter (NEW)
		List<string> selectedDynamicTags = new List<string>();
		foreach (var checkBox in _tagFilterCheckBoxes)
		{
			if (checkBox.ButtonPressed)
			{
				selectedDynamicTags.Add(checkBox.Text.ToLower());
			}
		}

		if (selectedDynamicTags.Any())
		{
			filtered = filtered.Where(novel =>
				novel.Tags != null && novel.Tags.Any(novelTag =>
					selectedDynamicTags.Contains(novelTag.ToLower())
				)
			);
		}

		// 5. Apply Status filter
		string selectedStatus = StatusFilterOptionButton.GetItemText(StatusFilterOptionButton.GetSelectedId());
		if (selectedStatus != "All")
		{
			filtered = filtered.Where(novel =>
				novel.PublicationStatus != null && novel.PublicationStatus == selectedStatus
			);
		}

		// 6. Apply Adult Content filter (using new OptionButton logic)
		string adultFilterOption = AdultContentFilterOptionButton.GetItemText(AdultContentFilterOptionButton.GetSelectedId());
		switch (adultFilterOption)
		{
			case "Show Adult Only":
				filtered = filtered.Where(novel => novel.IsAdult);
				break;
			case "Hide Adult":
				filtered = filtered.Where(novel => !novel.IsAdult);
				break;
			case "Show All": 
			default:
				break;
		}

		// 7. Apply Cover Status filter (using new OptionButton logic)
		string coverFilterOption = CoverStatusFilterOptionButton.GetItemText(CoverStatusFilterOptionButton.GetSelectedId());
		switch (coverFilterOption)
		{
			case "Show With Cover Only":
				filtered = filtered.Where(novel => HasValidCover(novel));
				break;
			case "Show Without Cover Only":
				filtered = filtered.Where(novel => !HasValidCover(novel));
				break;
			case "Show All": 
			default:
				break;
		}

		// 8. Apply Chapter Count filter
		int minChapters = 0;
		int maxChapters = int.MaxValue; 

		if (int.TryParse(MinChaptersLineEdit.Text.Trim(), out int parsedMinChapters))
		{
			minChapters = parsedMinChapters;
		}
		if (int.TryParse(MaxChaptersLineEdit.Text.Trim(), out int parsedMaxChapters))
		{
			maxChapters = parsedMaxChapters;
		}

		if (minChapters > maxChapters) 
		{
			GD.PushWarning($"Min Chapters ({minChapters}) is greater than Max Chapters ({maxChapters}). Swapping for filter.");
			int temp = minChapters;
			minChapters = maxChapters;
			maxChapters = temp;
		}

		filtered = filtered.Where(novel =>
			novel.ChapterCount >= minChapters && novel.ChapterCount <= maxChapters
		);

		// 9. Apply Like Count filter
		int minLikes = 0;
		int maxLikes = int.MaxValue; 

		if (int.TryParse(MinLikesLineEdit.Text.Trim(), out int parsedMinLikes))
		{
			minLikes = parsedMinLikes;
		}
		if (int.TryParse(MaxLikesLineEdit.Text.Trim(), out int parsedMaxLikes))
		{
			maxLikes = parsedMaxLikes;
		}

		if (minLikes > maxLikes) 
		{
			GD.PushWarning($"Min Likes ({minLikes}) is greater than Max Likes ({maxLikes}). Swapping for filter.");
			int temp = minLikes;
			minLikes = maxLikes;
			maxLikes = temp;
		}

		filtered = filtered.Where(novel =>
			novel.LikeCount >= minLikes && novel.LikeCount <= maxLikes
		);


		// --- Apply Sorting ---
		string sortBy = SortByOptionButton.GetItemText(SortByOptionButton.GetSelectedId());
		string sortOrder = SortOrderOptionButton.GetItemText(SortOrderOptionButton.GetSelectedId());

		switch (sortBy)
		{
			case "Title":
				filtered = (sortOrder == "Ascending") ? 
						   filtered.OrderBy(n => n.Title) : 
						   filtered.OrderByDescending(n => n.Title);
				break;
			case "Popularity (Likes)":
				filtered = (sortOrder == "Ascending") ? 
						   filtered.OrderBy(n => n.LikeCount) : 
						   filtered.OrderByDescending(n => n.LikeCount);
				break;
			case "Chapter Count":
				filtered = (sortOrder == "Ascending") ? 
						   filtered.OrderBy(n => n.ChapterCount) : 
						   filtered.OrderByDescending(n => n.ChapterCount);
				break;
			case "Relevance":
				if (!string.IsNullOrEmpty(generalSearchText))
				{
					// Relevance is always descending (highest score first)
					filtered = filtered.OrderByDescending(novel => CalculateRelevanceScore(novel, generalSearchText));
				}
				else
				{
					// Fallback to title sort if no search text for relevance
					filtered = (sortOrder == "Ascending") ? 
							   filtered.OrderBy(n => n.Title) : 
							   filtered.OrderByDescending(n => n.Title);
					GD.PushWarning("Relevance sort selected but no search text provided. Defaulting to Title sort.");
				}
				break;
			default:
				filtered = filtered.OrderBy(n => n.Title);
				break;
		}

		_currentFilteredNovels = filtered.ToList(); // Convert to List after all filtering and sorting
		
		// Reset to first page and update display for new filtered list
		_currentPage = 1;
		CalculateTotalPages();
		DisplayCurrentPage();

		// NEW DEBUG PRINT: Filtered novel count and total pages
		GD.Print($"[DEBUG] After filters: _currentFilteredNovels.Count = {_currentFilteredNovels.Count}, _totalPages = {_totalPages}");

		StatusLabel.Text = $"Displaying {_currentFilteredNovels.Count} novels matching filter criteria.";
	}

	// Helper method to calculate a simple relevance score
	private float CalculateRelevanceScore(NovelData novel, string searchText)
	{
		if (string.IsNullOrEmpty(searchText)) return 0;

		float score = 0;
		string lowerSearchText = searchText.ToLower();

		// Higher score for title matches
		if (novel.Title != null && novel.Title.ToLower().Contains(lowerSearchText))
		{
			score += 10;
			// Add more for exact title match or multiple occurrences
			if (novel.Title.ToLower() == lowerSearchText) score += 5;
			score += novel.Title.ToLower().Split(new string[] { lowerSearchText }, StringSplitOptions.None).Length - 1; // Count occurrences
		}

		// Moderate score for author matches
		if (novel.Author != null && novel.Author.ToLower().Contains(lowerSearchText))
		{
			score += 5;
			score += novel.Author.ToLower().Split(new string[] { lowerSearchText }, StringSplitOptions.None).Length - 1;
		}

		// Lower score for tag matches
		if (novel.Tags != null && novel.Tags.Any(tag => tag.ToLower().Contains(lowerSearchText)))
		{
			score += 2;
			score += novel.Tags.Count(tag => tag.ToLower().Contains(lowerSearchText)); // Count matching tags
		}

		// Lowest score for synopsis matches
		if (novel.Synopsis != null && novel.Synopsis.ToLower().Contains(lowerSearchText))
		{
			score += 1;
			score += novel.Synopsis.ToLower().Split(new string[] { lowerSearchText }, StringSplitOptions.None).Length - 1;
		}

		return score;
	}


	// --- Pagination Logic ---
	private void CalculateTotalPages()
	{
		if (_currentFilteredNovels.Count == 0 || NovelsPerPage <= 0)
		{
			_totalPages = 1;
		}
		else
		{
			_totalPages = (int)Math.Ceiling((double)_currentFilteredNovels.Count / NovelsPerPage);
		}
		// Ensure current page is not out of bounds after filtering
		if (_currentPage > _totalPages)
		{
			_currentPage = _totalPages;
		}
		if (_currentPage < 1 && _totalPages > 0)
		{
			_currentPage = 1;
		}
	}

	private void DisplayCurrentPage()
	{
		ClearNovelDisplay(); // Clear old nodes

		if (_currentFilteredNovels.Count == 0)
		{
			UpdatePageNumberLabel();
			UpdatePaginationButtons();
			return;
		}

		int startIndex = (_currentPage - 1) * NovelsPerPage;
		int endIndex = Math.Min(startIndex + NovelsPerPage, _currentFilteredNovels.Count);

		// Adjust GridContainer's height to fit the current page's content
		// Calculate rows for the current page
		int currentNodesCount = endIndex - startIndex;
		// Ensure NovelListContainer.Columns is not zero to prevent division by zero
		int columns = NovelListContainer.Columns > 0 ? NovelListContainer.Columns : 1;
		int currentRows = (int)Math.Ceiling((double)currentNodesCount / columns);
		float currentContentHeight = currentRows * (NovelEntryMinSize.Y + NovelListContainer.GetThemeConstant("v_separation"));
		
		// Set the GridContainer's CustomMinimumSize.Y to fit the current page's content
		NovelListContainer.CustomMinimumSize = new Vector2(NovelListContainer.CustomMinimumSize.X, currentContentHeight);
		// Ensure the GridContainer's VSizeFlags are set to ShrinkBegin so it respects its CustomMinimumSize
		NovelListContainer.SetVSizeFlags(Control.SizeFlags.ShrinkBegin);


		for (int i = startIndex; i < endIndex; i++)
		{
			NovelData novelDataResource = _currentFilteredNovels[i];
			var novelEntryPanel = CreateNovelEntryPanel(novelDataResource);
			NovelListContainer.AddChild(novelEntryPanel);
		}

		UpdatePageNumberLabel();
		UpdatePaginationButtons();
	}

	private void _on_PrevPageButton_Pressed()
	{
		if (_currentPage > 1)
		{
			_currentPage--;
			DisplayCurrentPage();
		}
	}

	private void _on_NextPageButton_Pressed()
	{
		if (_currentPage < _totalPages)
		{
			_currentPage++;
			DisplayCurrentPage();
		}
	}

	// New method to handle direct page input
	private void _on_GoToPageButton_Pressed()
	{
		if (int.TryParse(GoToPageLineEdit.Text, out int pageNumber))
		{
			if (pageNumber >= 1 && pageNumber <= _totalPages)
			{
				_currentPage = pageNumber;
				DisplayCurrentPage();
				StatusLabel.Text = $"Navigated to page {pageNumber}.";
			}
			else
			{
				StatusLabel.Text = $"Invalid page number. Please enter a number between 1 and {_totalPages}.";
				GD.PushWarning($"User attempted to go to invalid page: {pageNumber}. Valid range: 1-{_totalPages}");
			}
		}
		else
		{
			StatusLabel.Text = "Invalid input. Please enter a numerical page number.";
			GD.PushWarning($"User entered non-numeric page input: {GoToPageLineEdit.Text}");
		}
		// Clear the input field after attempt
		GoToPageLineEdit.Clear();
	}


	private void UpdatePageNumberLabel()
	{
		PageNumberLabel.Text = $"Page {_currentPage} of {_totalPages}";
	}

	private void UpdatePaginationButtons()
	{
		PrevPageButton.Disabled = (_currentPage <= 1);
		NextPageButton.Disabled = (_currentPage >= _totalPages);
	}

	// Helper method to create a single novel entry panel
	private PanelContainer CreateNovelEntryPanel(NovelData novelDataResource)
	{
		var novelEntryPanel = new PanelContainer();
		novelEntryPanel.CustomMinimumSize = NovelEntryMinSize;
		novelEntryPanel.SetHSizeFlags(Control.SizeFlags.ExpandFill); 
		novelEntryPanel.SetVSizeFlags(Control.SizeFlags.ExpandFill);
		novelEntryPanel.AddThemeConstantOverride("panel_margin_left", 5);
		novelEntryPanel.AddThemeConstantOverride("panel_margin_top", 5);
		novelEntryPanel.AddThemeConstantOverride("panel_margin_right", 5);
		novelEntryPanel.AddThemeConstantOverride("panel_margin_bottom", 5);

		var mainVBox = new VBoxContainer();
		mainVBox.AddThemeConstantOverride("separation", 5);
		mainVBox.SetHSizeFlags(Control.SizeFlags.ExpandFill);
		mainVBox.SetVSizeFlags(Control.SizeFlags.ExpandFill);
		novelEntryPanel.AddChild(mainVBox);

		var contentHBox = new HBoxContainer();
		contentHBox.AddThemeConstantOverride("separation", 10);
		contentHBox.SetHSizeFlags(Control.SizeFlags.ExpandFill);
		contentHBox.SetVSizeFlags(Control.SizeFlags.ExpandFill);
		mainVBox.AddChild(contentHBox); // Add this HBox to the main VBox

		// 1. Cover Image
		var coverTextureRect = new TextureRect();
		coverTextureRect.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
		coverTextureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		coverTextureRect.CustomMinimumSize = new Vector2(150, 225);
		coverTextureRect.SetHSizeFlags(Control.SizeFlags.ShrinkBegin);
		coverTextureRect.SetVSizeFlags(Control.SizeFlags.ShrinkBegin);

		bool coverLoaded = false;
		bool isCoverPathProvided = !string.IsNullOrEmpty(novelDataResource.CoverLocalPath);
		string originalCoverPath = novelDataResource.CoverLocalPath;
		string placeholderText = "No Cover";

		if (isCoverPathProvided)
		{
			var image = new Image();
			Error loadErr = Error.Failed;
			bool fileFoundAtAnyAttempt = false;

			if (Godot.FileAccess.FileExists(originalCoverPath))
			{
				fileFoundAtAnyAttempt = true;
				loadErr = image.Load(originalCoverPath);
				if (loadErr == Error.Ok)
				{
					coverLoaded = true;
				}
			}

			if (!coverLoaded)
			{
				string alternatePath = null;
				string originalExtension = Path.GetExtension(originalCoverPath).ToLower();

				if (originalExtension == ".png")
				{
					alternatePath = Path.ChangeExtension(originalCoverPath, ".jpg");
				}
				else if (originalExtension == ".jpg" || originalExtension == ".jpeg")
				{
					alternatePath = Path.ChangeExtension(originalCoverPath, ".png");
				}

				if (alternatePath != null)
				{
					if (Godot.FileAccess.FileExists(alternatePath))
					{
						fileFoundAtAnyAttempt = true;
						loadErr = image.Load(alternatePath);
						if (loadErr == Error.Ok)
						{
							coverLoaded = true;
						}
					}
				}
			}

			if (!coverLoaded && originalCoverPath.ToLower().EndsWith(".png"))
			{
				if (Godot.FileAccess.FileExists(originalCoverPath))
				{
					fileFoundAtAnyAttempt = true;
					byte[] buffer = null;
					try
					{
						using (var file = Godot.FileAccess.Open(originalCoverPath, Godot.FileAccess.ModeFlags.Read))
						{
							if (file != null && !file.EofReached())
							{
								buffer = file.GetBuffer((long)file.GetLength());
							}
						}
					}
					catch (Exception e)
					{
						GD.PushWarning($"Exception reading file {originalCoverPath} into buffer for PNG fallback: {e.Message}");
					}

					if (buffer != null)
					{
						image = new Image();
						loadErr = image.LoadPngFromBuffer(buffer);
						if (loadErr == Error.Ok)
						{
							coverLoaded = true;
						}
						else
						{
							GD.PushWarning($"Failed to load PNG from buffer (fallback): {originalCoverPath}, Error: {loadErr}. This often means the PNG file is malformed or non-standard for Godot's strict parser. Consider re-saving the image in a reliable image editor.");
						}
					}
				}
			}
			
			if (coverLoaded)
			{
				var imageTexture = ImageTexture.CreateFromImage(image);
				coverTextureRect.Texture = imageTexture;
			}
			else
			{
				if (fileFoundAtAnyAttempt)
				{
					placeholderText = "Cover Load Failed";
				}
				else
				{
					placeholderText = "Cover Missing";
				}
			}
		} 
		
		if (!coverLoaded)
		{
			coverTextureRect.Texture = _placeholderTexture;
			var placeholderLabel = new Label();
			placeholderLabel.HorizontalAlignment = HorizontalAlignment.Center;
			placeholderLabel.VerticalAlignment = VerticalAlignment.Center;
			placeholderLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			placeholderLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			
			placeholderLabel.Text = placeholderText;
			coverTextureRect.AddChild(placeholderLabel);
		}
		contentHBox.AddChild(coverTextureRect); // Add cover to content HBox

		// 2. Novel Info (Text)
		var infoVBox = new VBoxContainer();
		infoVBox.SetHSizeFlags(Control.SizeFlags.ExpandFill);
		infoVBox.SetVSizeFlags(Control.SizeFlags.ExpandFill);
		infoVBox.AddThemeConstantOverride("separation", 2);

		var titleLabel = new RichTextLabel();
		string displayTitle = string.IsNullOrEmpty(novelDataResource.Title) ? "Untitled Novel" : novelDataResource.Title;
		titleLabel.BbcodeEnabled = true;
		titleLabel.Text = $"[b]{displayTitle}[/b] [color=#808080][{novelDataResource.Id}][/color]";
		titleLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		titleLabel.CustomMinimumSize = new Vector2(0, 50);
		titleLabel.SetVSizeFlags(Control.SizeFlags.ShrinkBegin);
		titleLabel.SelectionEnabled = true;
		infoVBox.AddChild(titleLabel);

		if (!string.IsNullOrEmpty(novelDataResource.Author))
		{
			var authorLabel = new Label();
			authorLabel.Text = $"Author: {novelDataResource.Author}";
			authorLabel.AutowrapMode = TextServer.AutowrapMode.Word;
			authorLabel.SetVSizeFlags(Control.SizeFlags.ShrinkBegin);
			infoVBox.AddChild(authorLabel);
		}

		if (novelDataResource.ChapterCount > 0)
		{
			var chapterLabel = new Label();
			chapterLabel.Text = $"Chapters: {novelDataResource.ChapterCount}";
			chapterLabel.SetVSizeFlags(Control.SizeFlags.ShrinkBegin);
			infoVBox.AddChild(chapterLabel);
		}

		if (novelDataResource.LikeCount > 0)
		{
			var likeLabel = new Label();
			likeLabel.Text = $"Likes: {novelDataResource.LikeCount}";
			likeLabel.SetVSizeFlags(Control.SizeFlags.ShrinkBegin);
			infoVBox.AddChild(likeLabel);
		}

		if (novelDataResource.IsAdult)
		{
			var adultLabel = new RichTextLabel();
			adultLabel.BbcodeEnabled = true;
			adultLabel.Text = "[color=red]Adult Content[/color]";
			adultLabel.CustomMinimumSize = new Vector2(0, 25);
			adultLabel.SetVSizeFlags(Control.SizeFlags.ShrinkBegin);
			infoVBox.AddChild(adultLabel);
		}

		if (novelDataResource.Tags != null && novelDataResource.Tags.Any())
		{
			var tagsLabel = new Label();
			tagsLabel.Text = $"Tags: {string.Join(", ", novelDataResource.Tags)}";
			tagsLabel.AutowrapMode = TextServer.AutowrapMode.Word;
			tagsLabel.SetVSizeFlags(Control.SizeFlags.ShrinkBegin);
			infoVBox.AddChild(tagsLabel);
		}

		if (!string.IsNullOrEmpty(novelDataResource.PublicationStatus))
		{
			var statusLabel = new Label();
			statusLabel.Text = $"Status: {novelDataResource.PublicationStatus}";
			statusLabel.SetVSizeFlags(Control.SizeFlags.ShrinkBegin);
			infoVBox.AddChild(statusLabel);
		}

		if (!string.IsNullOrEmpty(novelDataResource.Synopsis))
		{
			var synopsisLabel = new RichTextLabel();
			synopsisLabel.BbcodeEnabled = true;
			string synopsisText = $"[color=#808080][b]Description:[/b]\n{novelDataResource.Synopsis}[/color]";
			synopsisLabel.Text = synopsisText;
			synopsisLabel.AutowrapMode = TextServer.AutowrapMode.Word;
			synopsisLabel.SetVSizeFlags(Control.SizeFlags.ExpandFill);
			synopsisLabel.CustomMinimumSize = new Vector2(0, 80);
			synopsisLabel.SelectionEnabled = true;
			infoVBox.AddChild(synopsisLabel);
		}
		
		contentHBox.AddChild(infoVBox); // Add info to content HBox


		// NEW: Buttons below the cover and info
		var buttonHBox = new HBoxContainer();
		buttonHBox.AddThemeConstantOverride("separation", 5);
		buttonHBox.SetHSizeFlags(Control.SizeFlags.ExpandFill);
		buttonHBox.SetVSizeFlags(Control.SizeFlags.ShrinkBegin); // Shrink to fit buttons
		mainVBox.AddChild(buttonHBox); // Add button HBox to the main VBox

		var addToCollectionButton = new Button();
		addToCollectionButton.Text = "Add to Collection";
		addToCollectionButton.SetHSizeFlags(Control.SizeFlags.ExpandFill); // Make buttons expand
		addToCollectionButton.Pressed += () => GD.Print($"'Add to Collection' pressed for Novel ID: {novelDataResource.Id}");
		buttonHBox.AddChild(addToCollectionButton);

		var downloadNovelButton = new Button();
		downloadNovelButton.Text = "Download Novel";
		downloadNovelButton.SetHSizeFlags(Control.SizeFlags.ExpandFill);
		downloadNovelButton.Pressed += () => DownloadNovel(novelDataResource.Id, novelDataResource.Title);;
		buttonHBox.AddChild(downloadNovelButton);

		var refreshButton = new Button();
		refreshButton.Text = "Refresh";
		refreshButton.SetHSizeFlags(Control.SizeFlags.ExpandFill);
		refreshButton.Pressed += () => GD.Print($"'Refresh' pressed for Novel ID: {novelDataResource.Id}");
		buttonHBox.AddChild(refreshButton);
		
		return novelEntryPanel;
	}

public void RefreshDisplay()
{
	GD.Print("Refreshing novel display with current settings.");
	// Recalculates layout based on potentially new NovelEntryMinSize
	DisplayCurrentPage(); 
}




	private void ClearNovelDisplay()
	{
		foreach (Node child in NovelListContainer.GetChildren())
		{
			child.QueueFree();
		}
	}


	private void DownloadNovel(string novelId, string title)
	{

	string executableDir = OS.GetExecutablePath().GetBaseDir();
	string downloaderExe = "NovelpiaDownloader.exe";
	string downloaderPath = Path.Combine(executableDir, "programs", downloaderExe);


	string safeFileName = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
	string outputFileName = $"{safeFileName}.html"; // Since you use the -html flag

	// Define the output directory inside the user data folder
	string outputDir = ProjectSettings.GlobalizePath("user://downloads");
	
	// Ensure the output directory exists
	Directory.CreateDirectory(outputDir);

	string outputPath = Path.Combine(outputDir, outputFileName);

	// 2. Check if the downloader executable exists
	if (!File.Exists(downloaderPath))
	{
		GD.PushError($"Downloader not found. Looked for it at: {downloaderPath}");
		return;
	}

	// 3. Assemble the arguments
	var arguments = new List<string>
	{
		"-autostart",
		"-novelid",
		novelId,
		"-html",
		"-output",
		outputPath
	};

	// 4. Execute the command
	GD.Print($"Running: {downloaderPath}");
	GD.Print($"Outputting to: {outputPath}");

// FIX: Change 'Error' to 'int' for the result variable
	int result = OS.Execute(downloaderPath, arguments.ToArray());

	// FIX: Check for 0 (success) instead of Error.Ok
	if (result != 0) // 0 typically means success for external processes
	{
		GD.PushError($"Failed to run downloader. Process exited with code: {result}");
	}
	else
	{
		GD.Print($"Downloader for novel {novelId} executed successfully. Check {outputPath}");
	}
	}




}

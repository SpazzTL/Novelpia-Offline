# first_launch_setup.gd
extends Node


const CONFIG_FILE_PATH = "user://novelpiaoffline.cfg"
const CONFIG_SECTION = "novelpiaoffline"
const FIRST_LAUNCH_KEY = "first_launch_done"

# --- Download Configuration ---
const METADATA_DOWNLOAD_URL = "https://github.com/SpazzTL/NovelpiaMetadata/raw/main/novelpia_metadata.jsonl"
const METADATA2_DOWNLOAD_URL = "https://github.com/SpazzTL/NovelpiaMetadata/raw/main/forbidden.txt"

const METADATA_SAVE_PATH = "user://novelpia_metadata.jsonl"
const METADATA2_SAVE_PATH = "user://forbidden.txt"

const COVERS_DOWNLOAD_URL = "https://github.com/SpazzTL/NovelpiaMetadata/tree/main/novelpia_covers" # This URL is for a GitHub tree, not a direct download. It will not download a file.


# --- Built-in Godot Functions ---
@onready var download_ui: Control = $DownloadUI
@onready var download_button_1: Button = $DownloadUI/DownloadButton1
@onready var download_button_2: Button = $DownloadUI/DownloadButton2
@onready var skip_button: Button = $DownloadUI/SkipButton
@onready var http_request_node: HTTPRequest = $DownloadUI/HTTPRequest
@onready var download_status_label: Label = $DownloadUI/DownloadStatusLabel
@onready var progressbar_1: ProgressBar = $DownloadUI/ProgressBar1
@onready var progressbar_2: ProgressBar = $DownloadUI/ProgressBar2

@export var covers_extraction_path: String = "user://novelpia_covers/" # Set this in the Inspector!

var _is_downloading = false # Flag to track if a download is active
# Updated: Tracks specific download states: "metadata1", "metadata2"
var _current_download_type = "" 

func _ready():
	download_ui.hide()
	print("Checking for first launch...")
	var config = ConfigFile.new()
	var err = config.load(CONFIG_FILE_PATH)

	# User has commented out these connections, assuming they are handled elsewhere or not desired.
	# if download_button_1 != null:
	# 	download_button_1.pressed.connect(_on_download_1_pressed)
	# if download_button_2 != null:
	# 	download_button_2.pressed.connect(_on_download_2_pressed)
	# if skip_button != null:
	# 	skip_button.pressed.connect(_on_skip_pressed)
	
	if http_request_node != null:
		print("HTTP Request Node type: ", http_request_node.get_class())
		
		# Set common HTTPRequest properties for better compatibility
		http_request_node.set_max_redirects(20) # Allow more redirects
		
		if http_request_node.has_signal("request_completed"):
			http_request_node.request_completed.connect(_on_HTTPRequest_request_completed)
		else:
			push_warning("HTTPRequest node does not have 'request_completed' signal. Check node type/version.")
	else:
		push_error("HTTP Request Node is null in _ready(). Check scene path: $DownloadUI/HTTPRequest")


	# Initialize progress bars
	if progressbar_1 != null:
		progressbar_1.value = 0
		progressbar_1.max_value = 1
		progressbar_1.visible = false
	if progressbar_2 != null:
		progressbar_2.value = 0
		progressbar_2.max_value = 1
		progressbar_2.visible = false

	# Check if the config file exists and if the 'first_launch_done' key is true
	if err == OK:
		if config.has_section_key(CONFIG_SECTION, FIRST_LAUNCH_KEY) and config.get_value(CONFIG_SECTION, FIRST_LAUNCH_KEY):
			# Not the first launch, proceed to main scene
			print("Not first launch. Loading main scene...")
			_load_main_scene()
			return
		
	# If we reach here, it's the first launch (or the flag wasn't set to true)
	print("First launch detected! Performing initial setup...")
	_do_first_launch_setup(config)

func _process(_delta):
	# Update progressbar_1 if a download is active
	if _is_downloading and http_request_node != null and progressbar_1 != null:
		var downloaded_bytes = http_request_node.get_downloaded_bytes()
		var total_bytes = http_request_node.get_body_size()

		if total_bytes > 0:
			progressbar_1.max_value = total_bytes
			progressbar_1.value = downloaded_bytes
			if download_status_label != null:
				download_status_label.text = "Downloading: %d%%" % (int(float(downloaded_bytes) / total_bytes * 100))
		else:
			progressbar_1.value = 0
			progressbar_1.max_value = 1
			if download_status_label != null:
				download_status_label.text = "Downloading (unknown size)..."


# --- Custom Functions ---

func _do_first_launch_setup(_config: ConfigFile):
	"""
	Handles actions to be performed on the very first launch of the game.
	"""
	print("--- First Launch Setup Actions ---")
	download_resources()
	if progressbar_2 != null:
		progressbar_2.value = 0
		progressbar_2.visible = true

func download_resources():
	"""
	Shows the download UI and initiates the download.
	"""
	download_ui.show()
	if download_status_label != null:
		download_status_label.text = "Ready to download metadata. Press a download button."
	if progressbar_1 != null:
		progressbar_1.value = 0
		progressbar_1.visible = true

func _on_download_1_pressed():
	"""
	Initiates the download of the first metadata file, then the second.
	"""
	if http_request_node == null:
		push_error("HTTP Request Node is not assigned!")
		if download_status_label != null:
			download_status_label.text = "Error: HTTP Request Node missing."
		return

	_current_download_type = "metadata1" # Set download type for the first metadata
	if http_request_node.request(METADATA_DOWNLOAD_URL) == OK:
		_is_downloading = true
		if download_status_label != null:
			download_status_label.text = "Downloading metadata 1..."
		print("Started downloading: %s" % METADATA_DOWNLOAD_URL)
		_disable_download_buttons()
		if progressbar_1 != null:
			progressbar_1.value = 0
	else:
		if download_status_label != null:
			download_status_label.text = "Failed to start download."
		push_error("Failed to start HTTP request for metadata 1.")
		# If first download fails, re-enable buttons and clear state
		_is_downloading = false
		_enable_download_buttons()
		_current_download_type = ""

func _on_download_2_pressed():
	"""
	This button is currently not used for any download logic as per user request.
	It remains a placeholder.
	"""
	OS.shell_open("https://pixeldrain.com/u/JFQS1V2c")


func _on_HTTPRequest_request_completed(result, response_code, _headers, body):
	"""
	Handles the completion of the HTTP download request.
	"""
	_is_downloading = false # Download finished (for this request)
	
	if progressbar_1 != null:
		progressbar_1.visible = false

	var config = ConfigFile.new()
	var _err = config.load(CONFIG_FILE_PATH)

	var current_type = _current_download_type # Store current type before potentially changing it
	_current_download_type = "" # Clear type immediately to prevent re-entry issues

	if result == HTTPRequest.RESULT_SUCCESS and response_code == 200:
		var save_path = ""
		var success_message = ""

		if current_type == "metadata1":
			save_path = METADATA_SAVE_PATH
			success_message = "Metadata 1 download successful! Saved to: %s" % save_path
		elif current_type == "metadata2":
			save_path = METADATA2_SAVE_PATH
			success_message = "Metadata 2 download successful! Saved to: %s" % save_path
		else:
			push_error("Unknown download type completed: %s" % current_type)
			if download_status_label != null:
				download_status_label.text = "Download failed: Unknown type."
			if progressbar_2 != null: progressbar_2.value = 0
			_enable_download_buttons() # Re-enable buttons on unknown type error
			return

		var file = FileAccess.open(save_path, FileAccess.WRITE)
		if file != null:
			file.store_buffer(body)
			file.close()
			
			if download_status_label != null:
				download_status_label.text = success_message
			print(success_message)

			if current_type == "metadata1":
				# If first metadata downloaded, start the second metadata download
				_current_download_type = "metadata2"
				if http_request_node.request(METADATA2_DOWNLOAD_URL) == OK:
					_is_downloading = true
					if download_status_label != null:
						download_status_label.text = "Downloading metadata 2..."
					print("Started downloading: %s" % METADATA2_DOWNLOAD_URL)
					# Buttons remain disabled as another download is active
					if progressbar_1 != null:
						progressbar_1.value = 0
				else:
					if download_status_label != null:
						download_status_label.text = "Failed to start metadata 2 download."
					push_error("Failed to start HTTP request for metadata 2.")
					_enable_download_buttons() # Re-enable if chaining fails
					# Don't set FIRST_LAUNCH_KEY if sequence incomplete
			elif current_type == "metadata2":
				# Both metadata downloads are complete
				_set_first_launch_done_and_update_status(config)
				# NO AUTOMATIC SCENE CHANGE AFTER DOWNLOAD
				if download_status_label != null:
					download_status_label.text = "All metadata downloaded. Ready to proceed via Skip button."

		else:
			if download_status_label != null:
				download_status_label.text = "Download failed: Could not save file %s." % save_path
			push_error("Failed to save file: %s" % save_path)
			if progressbar_2 != null: progressbar_2.value = 0
			_enable_download_buttons() # Re-enable buttons on save error
	else:
		var error_message = "Download failed. Result: %d, Response Code: %d" % [result, response_code]
		if download_status_label != null:
			download_status_label.text = error_message
		push_error(error_message)
		
		print("HTTP Request Result: %d, Response Code: %d" % [result, response_code])

		if body.size() > 0:
			var body_string = body.get_string_from_utf8()
			print("Response body on error: %s" % body_string)
		
		if progressbar_2 != null: progressbar_2.value = 0
		_enable_download_buttons() # Re-enable buttons on download failure

func _set_first_launch_done_and_update_status(config: ConfigFile):
	"""Helper to set first launch flag and update final status/progress."""
	config.set_value(CONFIG_SECTION, FIRST_LAUNCH_KEY, true)
	config.save(CONFIG_FILE_PATH)
	print("First launch flag saved.")
	
	if progressbar_2 != null:
		progressbar_2.value = 1
		progressbar_2.visible = false
	
	if download_status_label != null:
		download_status_label.text = "All downloads complete. Ready to proceed via Skip button."
	_enable_download_buttons() # Re-enable all buttons once all steps are done


# Removed _extract_zip_file as it's no longer needed for this functionality.


func _on_skip_pressed():
	"""
	Skips the download/setup and proceeds to the main scene, marking first launch as done.
	This is now the ONLY way to load the main scene from this screen.
	"""
	print("Skipping download/setup. Loading main scene...")
	var config = ConfigFile.new()
	var _err = config.load(CONFIG_FILE_PATH)
	
	config.set_value(CONFIG_SECTION, FIRST_LAUNCH_KEY, true)
	config.save(CONFIG_FILE_PATH)
	print("First launch flag saved (skipped download/setup).")
	
	if progressbar_1 != null:
		progressbar_1.visible = false
	if progressbar_2 != null:
		progressbar_2.value = 1
		progressbar_2.visible = false
	
	_enable_download_buttons() # Ensure buttons are re-enabled
	if download_status_label != null:
		download_status_label.text = "Ready to continue."
	
	_load_main_scene() # Load main scene directly after skipping


func _load_main_scene():
	"""
	Loads the main game scene.
	"""
	var main_scene_path = "res://Scenes/main.tscn"
	if not FileAccess.file_exists(main_scene_path):
		print("Warning: main.tscn not found at '%s'. Please create it or adjust the path." % main_scene_path)
		get_tree().quit()
		return

	download_ui.hide()
	get_tree().change_scene_to_file(main_scene_path)

func _disable_download_buttons():
	if download_button_1 != null: download_button_1.disabled = true
	if download_button_2 != null: download_button_2.disabled = true
	if skip_button != null: skip_button.disabled = true

func _enable_download_buttons():
	if download_button_1 != null: download_button_1.disabled = false
	if download_button_2 != null: download_button_2.disabled = false
	if skip_button != null: skip_button.disabled = false


func _on_folder_button_pressed() -> void:
	var user_data_path = ProjectSettings.globalize_path("user://")
	print("Attempting to open user folder: %s" % user_data_path)
	var err = OS.shell_open(user_data_path)
	if err != OK:
		push_error("Failed to open user folder: %s (Error: %d)" % [user_data_path, err])
		if download_status_label != null:
			download_status_label.text = "Error: Could not open user folder."

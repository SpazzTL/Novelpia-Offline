
extends Node


const GITHUB_OWNER = "SpazzTL"  
const GITHUB_REPO = "Novelpia-Offline"      

const CURRENT_GAME_VERSION = "0.1.0" 


const GITHUB_API_URL = "https://api.github.com/repos/%s/%s/releases/latest"


var http_request: HTTPRequest = null


signal update_check_completed(update_info) # Emitted when the check is done

# --- Built-in Godot Functions ---

@onready var update_ui: CanvasLayer = $"../UpdateUI"
@onready var ui: CanvasLayer = $"../UI"

func _ready():
	update_ui.hide()
	http_request = HTTPRequest.new()
	add_child(http_request)
	http_request.request_completed.connect(_on_http_request_completed)


	print("Starting GitHub update check...")
	check_for_updates()


func check_for_updates():
	"""
	Initiates an HTTP request to GitHub's API to fetch the latest release.
	"""
	var url = GITHUB_API_URL % [GITHUB_OWNER, GITHUB_REPO]
	print("Checking for updates at: %s" % url)

	# Clear any previous requests and make a new one
	var error = http_request.request(url)
	if error != OK:
		var error_info = {
			"error": "Failed to start HTTP request.",
			"details": "Error code: %s" % error
		}
		print("Error: %s" % error_info.error)
		emit_signal("update_check_completed", error_info)

func _on_http_request_completed(result: int, response_code: int, _headers: PackedStringArray, body: PackedByteArray):
	"""
	Handles the completion of the HTTP request.
	Parses the response and compares versions.
	"""
	var update_info = {}

	if result != HTTPRequest.RESULT_SUCCESS:
		update_info = {
			"error": "HTTP request failed.",
			"details": "Result code: %s, Response code: %s" % [result, response_code]
		}
		print("Error: %s" % update_info.error)
		emit_signal("update_check_completed", update_info)
		return

	if response_code != 200:
		update_info = {
			"error": "GitHub API returned an error.",
			"details": "Response code: %s, Body: %s" % [response_code, body.get_string_from_utf8()]
		}
		print("Error: %s" % update_info.error)
		emit_signal("update_check_completed", update_info)
		return

	var json_string = body.get_string_from_utf8()
	var json_parse_result = JSON.parse_string(json_string)

	if json_parse_result == null:
		update_info = {
			"error": "Failed to parse JSON response.",
			"details": "Invalid JSON: %s" % json_string
		}
		print("Error: %s" % update_info.error)
		emit_signal("update_check_completed", update_info)
		return

	var release_data = json_parse_result

	# Extract the latest version tag (e.g., "v1.0.1" or "1.0.1")
	var latest_version_tag = release_data.get("tag_name")
	if not latest_version_tag:
		update_info = {
			"error": "Could not find 'tag_name' in release data.",
			"details": release_data
		}
		print("Error: %s" % update_info.error)
		emit_signal("update_check_completed", update_info)
		return

	# Remove 'v' prefix if present for cleaner version comparison
	var latest_version = latest_version_tag
	if latest_version.begins_with("v"):
		latest_version = latest_version.substr(1)

	var release_url = release_data.get("html_url")

	print("Current game version: %s" % CURRENT_GAME_VERSION)
	print("Latest GitHub release version: %s" % latest_version)


	# Compare versions
	if _compare_versions(latest_version, CURRENT_GAME_VERSION) > 0:
		print("Update available!")
		update_info = {
			"update_available": true,
			"latest_version": latest_version,
			"release_url": release_url
		}
		update_ui.show()
		ui.hide()
	else:
		print("No update available. You are on the latest version or a newer one.")
		update_info = {
			"update_available": false,
			"latest_version": latest_version,
			"release_url": release_url
		}

	emit_signal("update_check_completed", update_info)


func _compare_versions(version1: String, version2: String) -> int:
	"""
	Compares two version strings (e.g., "1.2.3" vs "1.2.4").
	Returns:
	-1 if version1 < version2
	 0 if version1 == version2
	 1 if version1 > version2
	"""
	var parts1 = version1.split(".")
	var parts2 = version2.split(".")

	var max_len = maxi(parts1.size(), parts2.size())

	for i in range(max_len):
		var p1 = 0
		if i < parts1.size():
			p1 = int(parts1[i])

		var p2 = 0
		if i < parts2.size():
			p2 = int(parts2[i])

		if p1 < p2:
			return -1
		elif p1 > p2:
			return 1
	return 0 # Versions are equal


# --- Example Usage (for demonstration in another script or main scene) ---
# You can connect to the 'update_check_completed' signal from another script
# or directly use the 'update_info' dictionary returned by the signal.

# Example of how you might use the signal in another script (e.g., your Main scene):
#
# var update_checker_node = get_node("YourUpdateCheckerNodePath")
# if update_checker_node:
#     update_checker_node.update_check_completed.connect(_on_update_check_done)
#
# func _on_update_check_done(update_info: Dictionary):
#     if update_info.has("error"):
#         print("Update check error: %s - %s" % [update_info.error, update_info.details])
#         # Show an error message to the user
#     elif update_info.update_available:
#         print("New version available: %s" % update_info.latest_version)
#         print("Download from: %s" % update_info.release_url)
#         # Show a UI prompt to the user: "Update Available! Download now?"
#         # You can use OS.shell_open(update_info.release_url) to open the URL
#     else:
#         print("Game is up to date.")
#         # Optionally show "No updates available" message


func _on_download_update_pressed() -> void:
	OS.shell_open("https://github.com/SpazzTL/Novelpia-Offline/releases")


func _on_skip_update_pressed() -> void:
	update_ui.hide()
	ui.show()

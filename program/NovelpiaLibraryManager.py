import sys
import os
import platform
import datetime
import subprocess

# --- Automatic Dependency Installation Check ---
required_packages = {
    "requests": "requests",
    "bs4": "beautifulsoup4", # Package name for pip is 'beautifulsoup4'
    "selenium": "selenium",
    "webdriver_manager": "webdriver-manager"
}

missing_packages = []
for module_name, pip_name in required_packages.items():
    try:
        __import__(module_name)
    except ImportError:
        missing_packages.append(pip_name)

if missing_packages:
    print("Detected missing dependencies. Attempting to install them automatically...")
    print(f"Missing: {', '.join(missing_packages)}")
    try:
        # Use sys.executable to ensure pip is run for the current Python interpreter
        # Add --user for user-specific installation if permissions are an issue
        subprocess.check_call([sys.executable, "-m", "pip", "install", *missing_packages])
        print("Dependencies installed successfully. Please restart the script.")
        sys.exit(0) # Exit after installation, user should restart to ensure imports work
    except subprocess.CalledProcessError as e:
        print(f"ERROR: Failed to install dependencies automatically: {e}")
        print("Please ensure you have an active internet connection.")
        print("You might need to run the script from an administrator command prompt, or manually install:")
        print(f"  pip install {' '.join(missing_packages)}")
        sys.exit(1)
    except Exception as e:
        print(f"An unexpected error occurred during dependency installation: {e}")
        sys.exit(1)

# Now that we're sure all dependencies are installed, import them
import requests
from bs4 import BeautifulSoup
import re
import time
from urllib.parse import quote

from selenium.webdriver.chrome.options import Options
from selenium import webdriver
from selenium.webdriver.chrome.service import Service as ChromeService
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from selenium.common.exceptions import TimeoutException, WebDriverException
from webdriver_manager.chrome import ChromeDriverManager


# --- Custom Logger Class ---
class Logger(object):
    """
    A custom logger that writes output to both stdout/stderr and a log file.
    The log file is cleared at the beginning of each script execution.
    """
    def __init__(self, filename="log.txt"):
        self.terminal = sys.stdout
        self.log_file_path = filename
        # Open in write mode ("w") to clear the file on each start
        self.log = open(filename, "w", encoding="utf-8") 

    def write(self, message):
        self.terminal.write(message)
        self.log.write(message)

    def flush(self):
        self.terminal.flush()
        self.log.flush()

    def __enter__(self):
        sys.stdout = self
        sys.stderr = self
        # Write a single start message with timestamp
        self.log.write(f"--- Log for session started: {datetime.datetime.now()} ---\n\n")
        self.log.flush() # Ensure this initial message is written immediately
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.log.write(f"\n--- Log for session ended: {datetime.datetime.now()} ---\n")
        self.log.close()
        sys.stdout = self.terminal
        sys.stderr = self.terminal
# --- End Custom Logger Class ---


def normalize_title(title):
    """
    Normalizes a novel title by removing punctuation and extra spaces.
    This is used for comparison, not for the actual search query or filename.
    """
    # Remove any character that is not a Korean character, alphanumeric, or whitespace.
    normalized = re.sub(r'[^\w\s\uAC00-\uD7A3]', '', title) 
    normalized = re.sub(r'\s+', ' ', normalized).strip() # Replace multiple spaces with single space
    return normalized

def find_chromedriver_path():
    """
    Uses webdriver_manager to automatically download and return the path to the correct chromedriver.
    This eliminates manual chromedriver management.
    """
    print("  Attempting to automatically manage ChromeDriver using webdriver_manager...")
    try:
        # This will download the correct chromedriver if not already present or outdated
        driver_path = ChromeDriverManager().install()
        print(f"  ChromeDriver managed by webdriver_manager: {driver_path}")
        return driver_path
    except Exception as e:
        print(f"ERROR: webdriver_manager failed to install/find ChromeDriver: {e}")
        print("  Please ensure you have Google Chrome installed and an active internet connection.")
        print("  You may need to manually download chromedriver.exe from https://chromedriver.chromium.org/downloads")
        print("  and place it in the same directory as this script or in your system PATH.")
        return None

def get_novel_id(novel_title, chromedriver_path):
    """
    Searches for a novel by title on Novelpia and returns its ID.

    Args:
        novel_title (str): The exact title of the novel to search for.
        chromedriver_path (str): The path to the chromedriver executable.

    Returns:
        str: The novel ID if found and matched, otherwise None.
    """
    base_search_url_prefix = "https://novelpia.com/search/all//1/"
    base_search_url_suffix = "?page=1&rows=30&novel_type=&start_count_book=&end_count_book=&novel_age=&start_days=&sort_col=last_viewdate&novel_genre=&block_out=0&block_stop=0&is_contest=0&is_complete=&is_challenge=0&list_display=list"

    encoded_title = quote(novel_title) # Keep original title for search query
    search_url = base_search_url_prefix + encoded_title + base_search_url_suffix

    print(f"\n--- Searching for: '{novel_title}' ---")
    print(f"Generated URL: {search_url}")

    # Use 'Options' directly 
    chrome_options = Options()
    chrome_options.add_argument("--headless")
    chrome_options.add_argument("--disable-gpu")
    chrome_options.add_argument("--no-sandbox")
    chrome_options.add_argument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36")
    
    # Set logging level to suppress verbose WebDriver output
    chrome_options.add_argument("--log-level=3") 

    webdriver_service = ChromeService(executable_path=chromedriver_path)
    driver = None

    try:
        driver = webdriver.Chrome(service=webdriver_service, options=chrome_options)
        print("  WebDriver initialized. Navigating to URL...")
        driver.get(search_url)
        print("  Navigation complete. Waiting for elements...")

        try:
            WebDriverWait(driver, 20).until(
                EC.presence_of_element_located((By.CSS_SELECTOR, 'div.rand-lists.list'))
            )
            print("  WebDriverWait successful: '.rand-lists.list' container found.")
        except TimeoutException:
            print("  WebDriverWait timed out: '.rand-lists.list' container not found within 20 seconds. This might indicate no results or a page structure change.")

        page_source = driver.page_source
        
        soup = BeautifulSoup(page_source, 'html.parser')

        no_results_phrases = ["검색 결과가 없습니다", "결과 없음"]
        found_no_results = False
        for phrase in no_results_phrases:
            if phrase in page_source:
                print(f"  Detected '{phrase}' in page source. Likely no search results.")
                found_no_results = True
                break
        
        if found_no_results:
            return None

        # Find all potential novel link tags
        novel_link_tags = soup.find_all('a', href=re.compile(r'/novel/\d+'))

        if novel_link_tags:
            normalized_input_title = normalize_title(novel_title)
            print(f"  Normalized input title for comparison: '{normalized_input_title}'")

            for novel_link_tag in novel_link_tags:
                title_h6_tag = novel_link_tag.find('h6')
                if title_h6_tag:
                    found_title = title_h6_tag.get_text().strip()
                    normalized_found_title = normalize_title(found_title)
                    print(f"  Comparing found title '{found_title}' (normalized: '{normalized_found_title}')")

                    if normalized_found_title == normalized_input_title:
                        novel_url = novel_link_tag['href']
                        match = re.search(r'/novel/(\d+)', novel_url)
                        if match:
                            print(f"  MATCH FOUND: Normalized titles match for '{novel_title}' and '{found_title}'.")
                            return match.group(1)
                        else:
                            print("  Error: ID regex match failed on found URL for a matching title.")
                else:
                    print(f"  No <h6> title tag found within a potential novel link.")
            
            # If loop finishes without a match
            print(f"  No exact normalized title match found among search results for '{novel_title}'.")
        else:
            print(f"  No <a> tag with href='/novel/ID' found on the page for '{novel_title}'. This could mean no results or changed HTML structure.")

        return None

    except WebDriverException as e:
        print(f"ERROR: A WebDriver error occurred for '{novel_title}': {e}")
        print("  This often means ChromeDriver is incompatible with your Chrome browser.")
        print("  webdriver_manager should handle this, but if it fails, please manually check:")
        print("  1. Your Chrome browser version (chrome://version/).")
        print("  2. Download the matching chromedriver.exe from https://chromedriver.chromium.org/downloads")
        print("  3. Place it in the same directory as this script.")
        return None
    except Exception as e:
        print(f"ERROR: An unexpected error occurred during Selenium processing for '{novel_title}': {e}")
        return None
    finally:
        if driver:
            driver.quit()

def process_novel_list(input_file, output_file):
    """
    Reads novel titles from an input file, finds their IDs, and writes to an output file.

    Args:
        input_file (str): Path to the input text file with one novel title per line.
        output_file (str): Path to the output text file (title,ID).
    """
    novel_titles = []
    try:
        with open(input_file, 'r', encoding='utf-8') as f:
            for line in f:
                title = line.strip()
                # Handle BOM if present
                if title.startswith('\ufeff'):
                    title = title[1:]
                if title:
                    novel_titles.append(title)
    except FileNotFoundError:
        print(f"ERROR: Input file not found at '{input_file}'")
        return
    except Exception as e:
        print(f"ERROR: Could not read input file '{input_file}': {e}")
        return

    # Find chromedriver once for the entire process
    chromedriver_path = find_chromedriver_path()
    if not chromedriver_path:
        print("\nFATAL ERROR: Chromedriver could not be found or installed. Exiting.")
        return

    results = []
    print(f"\nStarting to process {len(novel_titles)} novels...")
    for i, title in enumerate(novel_titles):
        print(f"\nProcessing novel {i+1}/{len(novel_titles)}: {title}")
        novel_id = get_novel_id(title, chromedriver_path) # Pass chromedriver_path to get_novel_id
        if novel_id:
            results.append(f"{title},{novel_id}")
            print(f"  SUCCESS: Found ID: {novel_id} for '{title}'")
        else:
            results.append(f"{title},ID_NOT_FOUND")
            print(f"  FAILED: ID Not Found for '{title}'")

        # Be polite to the server, especially with multiple searches
        time.sleep(1.0) # Increased sleep to 1 second for better reliability

    try:
        with open(output_file, 'w', encoding='utf-8', newline='') as f:
            for line in results:
                f.write(line + '\n')
        print(f"\nProcessing complete. Results saved to {output_file}")
    except Exception as e:
        print(f"ERROR: Could not write to output file '{output_file}': {e}")


def generate_book_names_file(directory_path, output_filename="BookNames.txt"):
    """
    Scans a directory for .epub files, extracts their names (without extension),
    and saves them to a specified text file.

    Args:
        directory_path (str): The path to the directory containing EPUB files.
        output_filename (str): The name of the output text file.
    
    Returns:
        str: The full path to the generated BookNames.txt file, or None if an error occurred.
    """
    book_names = []
    try:
        if not os.path.isdir(directory_path):
            print(f"ERROR: Directory not found: '{directory_path}'")
            return None

        for filename in os.listdir(directory_path):
            if filename.lower().endswith(".epub"):
                # Remove the .epub extension
                name_without_extension = os.path.splitext(filename)[0]
                book_names.append(name_without_extension)
        
        # Sort for consistent output
        book_names.sort()

        # Save to file in the current script's directory
        script_dir = os.path.dirname(os.path.abspath(sys.argv[0]))
        output_file_path = os.path.join(script_dir, output_filename)

        with open(output_file_path, 'w', encoding='utf-8', newline='') as f:
            for name in book_names:
                f.write(name + '\n')
        
        print(f"\nSuccessfully scanned {len(book_names)} EPUB files.")
        print(f"Book names saved to: '{output_file_path}'")
        return output_file_path

    except Exception as e:
        print(f"ERROR: An error occurred while generating book names: {e}")
        return None

if __name__ == "__main__":
    # Determine the log file path in the script's directory
    script_dir = os.path.dirname(os.path.abspath(sys.argv[0]))
    log_file_path = os.path.join(script_dir, "log.txt")

    # Use the custom Logger as a context manager
    with Logger(log_file_path):
        if len(sys.argv) == 3:
            # Mode 1: Generate Novel IDs from an input list (command-line arguments)
            input_novel_list_file = sys.argv[1]
            output_novel_ids_file = sys.argv[2]
            print("\n--- Mode: Generating Novel IDs from provided files ---")
            process_novel_list(input_novel_list_file, output_novel_ids_file)
        else:
            # Mode 2: Interactive flow - Generate Book Names THEN Generate Novel IDs
            print("\n--- Mode: Interactive Book Name and Novel ID Generation ---")
            print("This mode will first scan a directory for .epub files and then find their Novel IDs.")
            
            book_dir = input("Please enter the path to your book directory (e.g., C:\\MyBooks): ").strip()
            
            # Ensure the path is valid before proceeding
            while not os.path.isdir(book_dir):
                print(f"Invalid directory: '{book_dir}'. Please enter a valid path.")
                book_dir = input("Please enter the path to your book directory: ").strip()

            # Step 1: Generate BookNames.txt
            book_names_file_path = generate_book_names_file(book_dir, "BookNames.txt")
            
            if book_names_file_path:
                # Step 2: Automatically use BookNames.txt to generate NovelIDs.txt
                output_novel_ids_file = os.path.join(script_dir, "NovelIDs.txt")
                
                print(f"\n--- Proceeding to find Novel IDs using '{os.path.basename(book_names_file_path)}' ---")
                process_novel_list(book_names_file_path, output_novel_ids_file)
            else:
                print("\nBook name generation failed. Cannot proceed to find Novel IDs.")
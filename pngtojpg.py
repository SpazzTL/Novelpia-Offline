import os
from PIL import Image

def convert_png_to_jpg(source_dir, output_dir, quality=90, remove_original=False):
    """
    Converts all PNG files in a source directory (and its subdirectories) to JPG.

    Args:
        source_dir (str): The directory containing the PNG files.
        output_dir (str): The directory where the converted JPG files will be saved.
        quality (int): The quality for JPG compression (0-100). Higher is better quality, larger file size.
        remove_original (bool): If True, the original PNG file will be deleted after successful conversion.
    """
    if not os.path.exists(source_dir):
        print(f"Error: Source directory '{source_dir}' does not exist.")
        return

    os.makedirs(output_dir, exist_ok=True) # Create output directory if it doesn't exist

    print(f"Starting conversion from '{source_dir}' to '{output_dir}'...")
    print(f"JPG Quality: {quality}")
    if remove_original:
        print("Warning: Original PNG files will be removed after successful conversion.")

    converted_count = 0
    skipped_count = 0
    error_count = 0

    for root, _, files in os.walk(source_dir):
        for filename in files:
            if filename.lower().endswith(".png"):
                png_path = os.path.join(root, filename)
                
                # Create corresponding output path, maintaining subdirectory structure
                relative_path = os.path.relpath(png_path, source_dir)
                jpg_filename = os.path.splitext(relative_path)[0] + ".jpg"
                jpg_path = os.path.join(output_dir, jpg_filename)

                # Ensure output subdirectory exists
                os.makedirs(os.path.dirname(jpg_path), exist_ok=True)

                try:
                    with Image.open(png_path) as img:
                        # Convert to RGB mode if image has an alpha channel (RGBA)
                        # JPG does not support alpha, so it will be flattened to black.
                        # If transparency is critical, JPG is not the right format.
                        if img.mode == 'RGBA':
                            img = img.convert('RGB')
                        
                        img.save(jpg_path, "JPEG", quality=quality)
                        print(f"Converted: {png_path} -> {jpg_path}")
                        converted_count += 1

                        if remove_original:
                            os.remove(png_path)
                            print(f"Removed original: {png_path}")

                except Exception as e:
                    print(f"Error converting '{png_path}': {e}")
                    error_count += 1
            else:
                skipped_count += 1

    print("\n--- Conversion Summary ---")
    print(f"Total files converted: {converted_count}")
    print(f"Total files skipped (not PNG): {skipped_count}")
    print(f"Total files with errors: {error_count}")
    print("Conversion complete.")

# --- How to use the script ---
if __name__ == "__main__":
    # IMPORTANT: Replace these paths with your actual directories!
    # Example:
    # source_directory = "C:/Users/YourUser/Documents/GodotProjects/MyNovelApp/novelpia_covers"
    # output_directory = "C:/Users/YourUser/Documents/GodotProjects/MyNovelApp/novelpia_covers_jpg"
    
    # Or, if your covers are in the Godot project's user data directory:
    # source_directory = os.path.join(os.path.expanduser("~"), ".local/share/godot/app_userdata/YourAppName/novelpia_covers")
    # output_directory = os.path.join(os.path.expanduser("~"), ".local/share/godot/app_userdata/YourAppName/novelpia_covers_jpg")

    # For testing, you might use relative paths if the script is in your project root
    source_directory = "novelpia_covers" 
    output_directory = "novelpia_covers_jpg"

    # Set your desired JPG quality (0-100)
    jpg_quality = 85 # 85 is usually a good balance of quality and file size

    # Set to True if you want to delete the original PNGs after successful conversion
    delete_original_pngs = False 

    convert_png_to_jpg(source_directory, output_directory, jpg_quality, delete_original_pngs)


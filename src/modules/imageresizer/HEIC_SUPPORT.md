# HEIC/HEIF Image Support in Image Resizer

PowerToys Image Resizer now includes support for HEIC (High Efficiency Image Container) and HEIF (High Efficiency Image Format) files, which are commonly used on iOS and macOS devices.

## Requirements

To use HEIC/HEIF files with Image Resizer, you need to have the HEIC codec installed on your Windows PC. 

The Image Resizer will:

1. Automatically detect if you have HEIC files on your system
2. Check if the required codec is installed
3. Prompt you to install the codec if needed

## Installing the HEIC Codec

If Image Resizer detects HEIC files but no codec is installed, it will suggest installing the codec from the Microsoft Store. You can install either:

- [HEIF Image Extensions](https://www.microsoft.com/store/productId/9PMMSR1CGPWG) - The free version
- [HEVC Video Extensions](https://www.microsoft.com/store/productId/9NMZLZ57R3T7) - Paid version with additional functionality

## How it Works

When you right-click on a HEIC/HEIF file:
- If the codec is installed, the Image Resizer option will appear in the context menu
- If the codec is not installed, the option won't appear until you install the codec

When resizing HEIC files:
- By default, they will be converted to JPEG format
- You can specify other output formats in the Image Resizer interface

## Supported File Extensions

- .heic - High Efficiency Image Container
- .heif - High Efficiency Image Format

## Troubleshooting

If you don't see the Image Resizer option for HEIC/HEIF files even after installing the codec, try:

1. Restarting your computer
2. Checking if PowerToys is running as administrator
3. Ensuring the Image Resizer module is enabled in PowerToys Settings
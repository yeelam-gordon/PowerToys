#pragma once
#include <filesystem>
#include <vector>

void ZipFolder(std::filesystem::path zipPath, std::filesystem::path folderPath, const std::vector<std::filesystem::path>& excludedFiles = {});
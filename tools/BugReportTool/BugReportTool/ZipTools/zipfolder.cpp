#include "ZipFolder.h"
#include <common/utils/timeutil.h>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <format>
#include <stdexcept>
#include <wil/stl.h>
#include <wil/win32_helpers.h>

void ZipFolder(std::filesystem::path zipPath, std::filesystem::path folderPath, const std::vector<std::filesystem::path>& excludedFiles)
{
    const auto reportFilename{
        std::format("PowerToysReport_{0}.zip",
                    timeutil::format_as_local("%F-%H-%M-%S", timeutil::now()))
    };
    const auto finalReportFullPath{ zipPath / reportFilename };

    const auto tempReportFilename{ reportFilename + ".tmp" };
    const auto tempReportFullPath{ zipPath / tempReportFilename };

    // tar -c --format=zip -f "ReportFile.zip" *
    const auto executable{ wil::ExpandEnvironmentStringsW<std::wstring>(LR"(%WINDIR%\System32\tar.exe)") };
    auto commandline{ std::format(LR"("{0}" -c --format=zip -f "{1}")", executable, tempReportFullPath.wstring()) };
    for (const auto& file : excludedFiles)
    {
        std::wstring pattern;
        // tar exclusions are patterns; preserve literal characters in report filenames.
        for (const auto character : file.lexically_relative(folderPath).generic_wstring())
        {
            if (character == L'*' || character == L'?' || character == L'[' || character == L']' || character == L'\\')
            {
                pattern += L'\\';
            }
            pattern += character;
        }
        commandline += std::format(LR"( --exclude="{0}")", pattern);
    }
    commandline += L" *";

    const auto folderPathAsString{ folderPath.lexically_normal().wstring() };

    wil::unique_process_information pi;
    STARTUPINFOW si{ .cb = sizeof(STARTUPINFOW) };
    if (!CreateProcessW(executable.c_str(),
                        commandline.data() /* must be mutable */,
                        nullptr,
                        nullptr,
                        FALSE,
                        DETACHED_PROCESS,
                        nullptr,
                        folderPathAsString.c_str(),
                        &si,
                        &pi))
    {
        printf("Cannot open zip.");
        throw -1;
    }

    DWORD exitCode = 0;
    if (WaitForSingleObject(pi.hProcess, INFINITE) != WAIT_OBJECT_0 ||
        !GetExitCodeProcess(pi.hProcess, &exitCode) || exitCode != 0)
    {
        throw std::runtime_error("Failed to create report archive");
    }

    std::filesystem::rename(tempReportFullPath, finalReportFullPath);
}

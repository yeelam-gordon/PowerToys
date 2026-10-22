#include "pch.h"
#include <CppUnitTest.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    std::vector<wchar_t> clipboardText;
    SIZE_T clipboardBytes = 0;
    bool canOpen = true;
    bool hasText = true;
    bool hasData = true;
    bool canLock = true;
    int openCount = 0;
    int closeCount = 0;
    int lockCount = 0;
    int unlockCount = 0;
    int dataCount = 0;
    DWORD fixtureSequence = 1;

    BOOL TestOpenClipboard(HWND)
    {
        ++openCount;
        return canOpen;
    }

    BOOL TestCloseClipboard()
    {
        ++closeCount;
        return TRUE;
    }

    BOOL TestIsClipboardFormatAvailable(UINT format)
    {
        Assert::AreEqual(static_cast<UINT>(CF_UNICODETEXT), format);
        return hasText;
    }

    HANDLE TestGetClipboardData(UINT format)
    {
        Assert::AreEqual(static_cast<UINT>(CF_UNICODETEXT), format);
        ++dataCount;
        return hasData ? reinterpret_cast<HANDLE>(1) : nullptr;
    }

    SIZE_T TestGlobalSize(HGLOBAL)
    {
        return clipboardBytes;
    }

    LPVOID TestGlobalLock(HGLOBAL)
    {
        ++lockCount;
        return canLock ? clipboardText.data() : nullptr;
    }

    BOOL TestGlobalUnlock(HGLOBAL)
    {
        ++unlockCount;
        return TRUE;
    }

    DWORD TestGetClipboardSequenceNumber()
    {
        return fixtureSequence;
    }

    wchar_t* TestCopyClipboardText(wchar_t* destination, const wchar_t* source)
    {
        Assert::IsTrue(source == clipboardText.data());
        const auto end = std::find(clipboardText.begin(), clipboardText.end(), L'\0');
        Assert::IsTrue(end != clipboardText.end(), L"String copy requires a terminator within the supplied clipboard text.");
        const size_t length = static_cast<size_t>(end - clipboardText.begin());
        Assert::IsTrue(length < clipboardBytes / sizeof(wchar_t), L"String copy must fit within the supplied clipboard text.");
        return wcscpy(destination, source);
    }

    void SetText(const std::wstring& text, bool terminated = true)
    {
        clipboardText.assign(text.begin(), text.end());
        if (terminated)
        {
            clipboardText.push_back(L'\0');
        }
        clipboardBytes = clipboardText.size() * sizeof(wchar_t);
        ++fixtureSequence;
    }
}

// Compile the actual reader and prefix handling against isolated clipboard data.
#define OpenClipboard TestOpenClipboard
#define CloseClipboard TestCloseClipboard
#define IsClipboardFormatAvailable TestIsClipboardFormatAvailable
#define GetClipboardData TestGetClipboardData
#define GlobalSize TestGlobalSize
#define GlobalLock TestGlobalLock
#define GlobalUnlock TestGlobalUnlock
#define GetClipboardSequenceNumber TestGetClipboardSequenceNumber
// Fail a reader regression before attempting a string copy outside the fixture.
#define wcscpy TestCopyClipboardText
#include "..\ZoomIt\DemoType.cpp"
#undef OpenClipboard
#undef CloseClipboard
#undef IsClipboardFormatAvailable
#undef GetClipboardData
#undef GlobalSize
#undef GlobalLock
#undef GlobalUnlock
#undef GetClipboardSequenceNumber
#undef wcscpy

namespace DemoTypeClipboardTests
{
    TEST_CLASS (ClipboardTextTests)
    {
    public:
        TEST_METHOD_INITIALIZE(Initialize)
            {
                canOpen = hasText = hasData = canLock = true;
                openCount = closeCount = lockCount = unlockCount = dataCount = 0;
                g_Clipboard = false;
                g_LastClipboardSeq = 0;
                g_Text.clear();
                SetText(L"[start]Example");
            }

            TEST_METHOD_CLEANUP(Cleanup)
                {
                    delete[] g_ClipboardCache;
                    g_ClipboardCache = nullptr;
                    g_Clipboard = false;
                    g_Text.clear();
                }

                TEST_METHOD (PreservesUnicodeAndIndependentCopy)
                {
                    SetText(L"[start]\u4F60\u597D \U0001F600\r\n\tEnd");
                    std::unique_ptr<wchar_t[]> copy(GetClipboard());
                    Assert::IsNotNull(copy.get());
                    Assert::AreEqual(L"[start]\u4F60\u597D \U0001F600\r\n\tEnd", copy.get());
                    clipboardText[0] = L'X';
                    Assert::AreEqual(L'[', copy[0]);
                    Assert::AreEqual(1, closeCount);
                    Assert::AreEqual(1, unlockCount);
                }

                TEST_METHOD (TerminatesTextWithoutTrailingNull)
                {
                    SetText(L"[start]\u4F60\u597D \U0001F600", false);
                    std::unique_ptr<wchar_t[]> copy(GetClipboard());
                    Assert::AreEqual(L"[start]\u4F60\u597D \U0001F600", copy.get());
                    Assert::AreEqual(0, static_cast<int>(copy[clipboardText.size()]));
                    SetText(L"[start]Example", false);
                    Assert::IsTrue(GetDemoTypeClipboard());
                    Assert::AreEqual(L"Example", g_Text.c_str());
                    Assert::AreEqual(0, static_cast<int>(g_ClipboardCache[clipboardText.size()]));
                    Assert::AreEqual(openCount, closeCount);
                    Assert::AreEqual(2, unlockCount);
                }

                TEST_METHOD (StopsAtFirstNull)
                {
                    SetText(std::wstring(L"[start]First\0Unused", 19), false);
                    Assert::IsTrue(GetDemoTypeClipboard());
                    Assert::AreEqual(L"First", g_Text.c_str());
                }

                TEST_METHOD (IgnoresIncompleteTrailingCodeUnit)
                {
                    SetText(L"[start]TextX", false);
                    --clipboardBytes;
                    std::unique_ptr<wchar_t[]> copy(GetClipboard());
                    Assert::AreEqual(L"[start]Text", copy.get());
                }

                TEST_METHOD (RejectsShortOrIncorrectPrefix)
                {
                    for (const auto* text : { L"", L"[", L"[start", L"[start]", L"xstart]Text", L"[Start]Text" })
                    {
                        SetText(text);
                        Assert::IsFalse(GetDemoTypeClipboard(), text);
                        Assert::IsFalse(g_Clipboard);
                    }
                }

                TEST_METHOD (DoesNotReuseTextAfterRejectedClipboard)
                {
                    Assert::IsTrue(GetDemoTypeClipboard());
                    SetText(L"Other text");
                    Assert::IsFalse(GetDemoTypeClipboard());
                    Assert::IsTrue(g_Text.empty());
                }

                TEST_METHOD (ReusesUnchangedClipboardCache)
                {
                    Assert::IsTrue(GetDemoTypeClipboard());
                    const int reads = dataCount;
                    Assert::IsTrue(GetDemoTypeClipboard());
                    Assert::AreEqual(reads, dataCount);
                }

                TEST_METHOD (FailedOpenDoesNotReadOrClose)
                {
                    canOpen = false;
                    Assert::IsNull(GetClipboard());
                    Assert::AreEqual(static_cast<DWORD>(0), GetClipboardSequence());
                    Assert::AreEqual(0, closeCount);
                    Assert::AreEqual(0, dataCount);
                    Assert::AreEqual(0, lockCount);
                }

                TEST_METHOD (MissingFormatOrDataClosesWithoutLocking)
                {
                    hasText = false;
                    Assert::IsNull(GetClipboard());
                    Assert::AreEqual(0, dataCount);
                    hasText = true;
                    hasData = false;
                    Assert::IsNull(GetClipboard());
                    Assert::AreEqual(2, closeCount);
                    Assert::AreEqual(0, lockCount);
                    Assert::AreEqual(0, unlockCount);
                }

                TEST_METHOD (RejectsUnsupportedSizesWithoutLocking)
                {
                    for (const SIZE_T size : { SIZE_T{ 0 }, SIZE_T{ 1 }, SIZE_T{ MAX_INPUT_SIZE + 1 } })
                    {
                        clipboardBytes = size;
                        Assert::IsNull(GetClipboard());
                    }
                    Assert::AreEqual(3, closeCount);
                    Assert::AreEqual(0, lockCount);
                    Assert::AreEqual(0, unlockCount);
                }

                TEST_METHOD (FailedLockClosesWithoutUnlocking)
                {
                    canLock = false;
                    Assert::IsNull(GetClipboard());
                    Assert::AreEqual(1, closeCount);
                    Assert::AreEqual(1, lockCount);
                    Assert::AreEqual(0, unlockCount);
                }
    };
}

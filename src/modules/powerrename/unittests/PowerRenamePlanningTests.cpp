#include "pch.h"
#include <PowerRenameItem.h>
#include <PowerRenameRegEx.h>
#include <Renaming.h>
#include "MockPowerRenameItem.h"
#include <initializer_list>
#include <string>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace PowerRenamePlanningTests
{
    namespace
    {
        using Status = PowerRenameItemRenameStatus;

        struct PlanningCase
        {
            PCWSTR id;
            PCWSTR original;
            bool folder;
            DWORD flags;
            PCWSTR search;
            PCWSTR replacement;
            PCWSTR candidate;
            Status status;
            bool planned;
            bool eligible;
            unsigned long index;
        };

        CComPtr<IPowerRenameItem> MakeItem(PCWSTR original, bool folder = false, UINT depth = 0, PCWSTR path = nullptr)
        {
            const std::wstring fixturePath = std::wstring(L"C:\\fixtures\\") + original;
            CComPtr<IPowerRenameItem> item;
            Assert::AreEqual(S_OK, CMockPowerRenameItem::CreateInstance(path ? path : fixturePath.c_str(), original, depth, folder, SYSTEMTIME{}, &item));
            return item;
        }

        CComPtr<IPowerRenameRegEx> MakeEngine(bool useBoost, DWORD flags, PCWSTR search, PCWSTR replacement)
        {
            CComPtr<IPowerRenameRegEx> engine;
            Assert::AreEqual(S_OK, CPowerRenameRegEx::s_CreateInstance(useBoost, &engine));
            Assert::AreEqual(S_OK, engine->PutFlags(flags));
            Assert::AreEqual(S_OK, engine->PutSearchTerm(search));
            Assert::AreEqual(S_OK, engine->PutReplaceTerm(replacement));
            return engine;
        }

        void AssertItem(CComPtr<IPowerRenameItem>& item, DWORD flags, PCWSTR candidate, Status status, bool eligible, PCWSTR context)
        {
            PWSTR actual = nullptr;
            Assert::AreEqual(S_OK, item->GetNewName(&actual), context);
            const bool matches = candidate ? actual && std::wstring(candidate) == actual : actual == nullptr;
            CoTaskMemFree(actual);
            Assert::IsTrue(matches, context);

            Status actualStatus = Status::Init;
            Assert::AreEqual(S_OK, item->GetStatus(&actualStatus), context);
            Assert::AreEqual(static_cast<int>(status), static_cast<int>(actualStatus), context);
            bool actualEligible = false;
            Assert::AreEqual(S_OK, item->ShouldRenameItem(flags, &actualEligible), context);
            Assert::AreEqual(eligible, actualEligible, context);
        }
    }

    TEST_CLASS (RealItemTests)
    {
        CComPtr<IPowerRenameItem> MakeRealItem(PCWSTR original)
        {
            CComPtr<IPowerRenameItem> item;
            Assert::AreEqual(S_OK, CPowerRenameItem::s_CreateInstance(nullptr, IID_PPV_ARGS(&item)));
            Assert::AreEqual(S_OK, item->PutOriginalName(original));
            return item;
        }

        void AssertDecision(IPowerRenameItem * item, DWORD flags, bool eligible, bool applicable, bool selected, PCWSTR id)
        {
            bool actual = !eligible;
            Assert::AreEqual(S_OK, item->ShouldRenameItem(flags, &actual), id);
            Assert::AreEqual(eligible, actual, id);
            struct FilterCase
            {
                DWORD filter;
                bool expected;
            };
            const FilterCase filters[] = {
                { PowerRenameFilters::None, true },
                { PowerRenameFilters::Selected, selected },
                { PowerRenameFilters::FlagsApplicable, applicable },
                { PowerRenameFilters::ShouldRename, eligible },
            };
            for (const auto& test : filters)
            {
                actual = !test.expected;
                Assert::AreEqual(S_OK, item->IsItemVisible(test.filter, flags, &actual), id);
                Assert::AreEqual(test.expected, actual, id);
            }
        }

    public:
        TEST_METHOD (RealItemCandidateStatusAndSelectionTransitions)
        {
            struct Transition
            {
                PCWSTR id;
                PCWSTR candidate;
                Status status;
                bool selected;
                bool eligible;
            };
            const Transition transitions[] = {
                { L"ITEM-V1-01/init-stale", L"done.txt", Status::Init, true, false },
                { L"ITEM-V1-02/ready", L"done.txt", Status::ShouldRename, true, true },
                { L"ITEM-V1-03/deselected", L"done.txt", Status::ShouldRename, false, false },
                { L"ITEM-V1-04/reselected", L"done.txt", Status::ShouldRename, true, true },
                { L"ITEM-V1-05/too-long", L"done.txt", Status::ItemNameTooLong, true, false },
                { L"ITEM-V1-06/invalid", L"done.txt", Status::ItemNameInvalidChar, true, false },
                { L"ITEM-V1-07/collision", L"done.txt", Status::ItemNameAlreadyExists, true, false },
                { L"ITEM-V1-08/recovered", L"done.txt", Status::ShouldRename, true, true },
                { L"ITEM-V1-09/identity", L"report.txt", Status::ShouldRename, true, false },
                { L"ITEM-V1-10/empty", L"", Status::ShouldRename, true, false },
                { L"ITEM-V1-11/no-candidate", nullptr, Status::ShouldRename, true, false },
                { L"ITEM-V1-12/unmatched-unselected", nullptr, Status::ShouldRename, false, false },
            };
            auto item = MakeRealItem(L"report.txt");
            for (const auto& test : transitions)
            {
                Assert::AreEqual(S_OK, item->PutNewName(test.candidate), test.id);
                Assert::AreEqual(S_OK, item->PutStatus(test.status), test.id);
                Assert::AreEqual(S_OK, item->PutSelected(test.selected), test.id);
                AssertDecision(item, 0, test.eligible, true, test.selected, test.id);
            }
        }

        TEST_METHOD (RealItemFileExclusionAndDepthFilters)
        {
            struct Gate
            {
                PCWSTR id;
                int depth;
                DWORD flags;
                bool allowed;
            };
            const Gate gates[] = {
                { L"ITEM-V1-13/root", 0, 0, true },
                { L"ITEM-V1-14/exclude-files", 0, ExcludeFiles, false },
                { L"ITEM-V1-15/exclude-folders", 0, ExcludeFolders, true },
                { L"ITEM-V1-16/root-not-subfolder", 0, ExcludeSubfolders, true },
                { L"ITEM-V1-17/file-and-folder", 0, ExcludeFiles | ExcludeFolders, false },
                { L"ITEM-V1-18/file-and-subfolder", 0, ExcludeFiles | ExcludeSubfolders, false },
                { L"ITEM-V1-19/folder-and-subfolder-root", 0, ExcludeFolders | ExcludeSubfolders, true },
                { L"ITEM-V1-20/all-excludes", 0, ExcludeFiles | ExcludeFolders | ExcludeSubfolders, false },
                { L"ITEM-V1-21/nested-allowed", 1, 0, true },
                { L"ITEM-V1-22/nested-excluded", 1, ExcludeSubfolders, false },
                { L"ITEM-V1-23/deep-excluded", 3, ExcludeFolders | ExcludeSubfolders, false },
                { L"ITEM-V1-24/nested-file-unaffected-by-folder", 1, ExcludeFolders, true },
                { L"ITEM-V1-25/depth-restored", 0, ExcludeSubfolders, true },
            };
            auto item = MakeRealItem(L"report.txt");
            Assert::AreEqual(S_OK, item->PutNewName(L"done.txt"));
            Assert::AreEqual(S_OK, item->PutStatus(Status::ShouldRename));
            for (const auto& test : gates)
            {
                Assert::AreEqual(S_OK, item->PutDepth(test.depth), test.id);
                AssertDecision(item, test.flags, test.allowed, test.allowed, true, test.id);
            }
        }

        TEST_METHOD (RealItemThreeRowVisibilityPreservesIdentityAndOrder)
        {
            const PCWSTR originals[] = { L"match1.txt", L"match2.txt", L"keepme.txt" };
            const PCWSTR candidates[] = { L"renamed1.txt", L"renamed2.txt", nullptr };
            std::vector<CComPtr<IPowerRenameItem>> items;
            for (size_t i = 0; i < ARRAYSIZE(originals); ++i)
            {
                auto item = MakeRealItem(originals[i]);
                Assert::AreEqual(S_OK, item->PutNewName(candidates[i]));
                Assert::AreEqual(S_OK, item->PutStatus(Status::ShouldRename));
                items.push_back(item);
            }
            struct View
            {
                PCWSTR id;
                DWORD filter;
                std::vector<std::wstring> expected;
            };
            const View views[] = {
                { L"ITEM-V1-26/show-all", PowerRenameFilters::None, { L"match1.txt", L"match2.txt", L"keepme.txt" } },
                { L"ITEM-V1-27/rename-only", PowerRenameFilters::ShouldRename, { L"match1.txt", L"match2.txt" } },
                { L"ITEM-V1-28/show-all-again", PowerRenameFilters::None, { L"match1.txt", L"match2.txt", L"keepme.txt" } },
            };
            for (const auto& view : views)
            {
                std::vector<std::wstring> visible;
                for (const auto& item : items)
                {
                    bool show = false;
                    Assert::AreEqual(S_OK, item->IsItemVisible(view.filter, 0, &show), view.id);
                    if (show)
                    {
                        PWSTR original = nullptr;
                        Assert::AreEqual(S_OK, item->GetOriginalName(&original), view.id);
                        const std::wstring identity = original ? original : L"";
                        CoTaskMemFree(original);
                        visible.push_back(identity);
                    }
                }
                Assert::IsTrue(view.expected == visible, view.id);
            }
        }

        TEST_METHOD (RealItemResetClearsCandidateAndRestoresEligibilityAfterReplan)
        {
            auto item = MakeRealItem(L"report.txt");
            Assert::AreEqual(S_OK, item->PutNewName(L"done.txt"));
            Assert::AreEqual(S_OK, item->PutStatus(Status::ShouldRename));
            AssertDecision(item, 0, true, true, true, L"ITEM-V1-29/before-reset");
            Assert::AreEqual(S_OK, item->Reset());
            PWSTR candidate = nullptr;
            Assert::AreEqual(S_OK, item->GetNewName(&candidate));
            const bool cleared = candidate == nullptr;
            CoTaskMemFree(candidate);
            Assert::IsTrue(cleared, L"ITEM-V1-30/reset-clears-candidate");
            AssertDecision(item, 0, false, true, true, L"ITEM-V1-30/after-reset");
            Assert::AreEqual(S_OK, item->PutNewName(L"replanned.txt"));
            AssertDecision(item, 0, true, true, true, L"ITEM-V1-31/replanned");
        }
    };

    TEST_CLASS (PlanningTests)
    {
    public:
        TEST_METHOD (ScopeCaseAndNoMatchTuples)
        {
            const PlanningCase cases[] = {
                { L"PLAN-02/full", L"aaa.aaa", false, MatchAllOccurrences, L"aaa", L"zzz", L"zzz.zzz", Status::ShouldRename, true, true, 1 },
                { L"PLAN-02/name", L"aaa.aaa", false, NameOnly | MatchAllOccurrences, L"aaa", L"zzz", L"zzz.aaa", Status::ShouldRename, true, true, 1 },
                { L"PLAN-02/extension", L"aaa.aaa", false, ExtensionOnly | MatchAllOccurrences, L"aaa", L"zzz", L"aaa.zzz", Status::ShouldRename, true, true, 1 },
                { L"PLAN-02/extensionless-name", L"report", false, NameOnly, L"report", L"done", L"done", Status::ShouldRename, true, true, 1 },
                { L"PLAN-02/extensionless-extension", L"report", false, ExtensionOnly, L"report", L"done", nullptr, Status::ShouldRename, false, false, 0 },
                { L"PLAN-02/dotted-folder-name", L"aaa.aaa", true, NameOnly | MatchAllOccurrences, L"aaa", L"zzz", L"zzz.zzz", Status::ShouldRename, true, true, 1 },
                { L"PLAN-02/dotted-folder-extension", L"aaa.aaa", true, ExtensionOnly, L"aaa", L"zzz", nullptr, Status::Init, false, false, 0 },
                { L"PLAN-02/multiple-dot-name", L"aaa.aaa.txt", false, NameOnly | MatchAllOccurrences, L"aaa", L"zzz", L"zzz.zzz.txt", Status::ShouldRename, true, true, 1 },
                { L"PLAN-02/multiple-dot-extension", L"aaa.aaa.txt", false, ExtensionOnly, L"txt", L"log", L"aaa.aaa.log", Status::ShouldRename, true, true, 1 },
                { L"PLAN-03/upper", L"Hi World.txt", false, Uppercase, L"Hi World", L"Hi World", L"HI WORLD.TXT", Status::ShouldRename, true, true, 1 },
                { L"PLAN-03/lower", L"Hi World.txt", false, Lowercase, L"Hi World", L"Hi World", L"hi world.txt", Status::ShouldRename, true, true, 1 },
                { L"PLAN-03/title-identity", L"Hi World.txt", false, Titlecase, L"Hi World", L"Hi World", nullptr, Status::ShouldRename, false, false, 1 },
                { L"PLAN-03/title-changed", L"hI wORLD.txt", false, Titlecase, L"hI wORLD", L"Hi World", L"Hi World.txt", Status::ShouldRename, true, true, 1 },
                { L"PLAN-04/sensitive-no-match", L"MixedCase.txt", false, CaseSensitive, L"mixed", L"XXX", nullptr, Status::ShouldRename, false, false, 0 },
                { L"PLAN-04/insensitive-match", L"MixedCase.txt", false, 0, L"mixed", L"XXX", L"XXXCase.txt", Status::ShouldRename, true, true, 1 },
            };

            // These literal fixtures have the same contract on both engines, not regex-dialect equivalence.
            for (const bool useBoost : { false, true })
            {
                for (const auto& test : cases)
                {
                    const std::wstring context = std::wstring(test.id) + (useBoost ? L"/Boost" : L"/std");
                    auto engine = MakeEngine(useBoost, test.flags, test.search, test.replacement);
                    auto item = MakeItem(test.original, test.folder);
                    unsigned long index = 0;
                    Assert::AreEqual(test.planned, DoRename(engine, index, item), context.c_str());
                    Assert::AreEqual(test.index, index, context.c_str());
                    AssertItem(item, test.flags, test.candidate, test.status, test.eligible, context.c_str());
                }
            }
        }

        TEST_METHOD (IncludeCombinationsClearStaleCandidatesWithoutConsumingIndex)
        {
            struct Includes
            {
                DWORD flags;
                bool allowed[3];
            };
            const Includes combinations[] = {
                { 0, { true, true, true } },
                { ExcludeFiles, { false, true, false } },
                { ExcludeFolders, { true, false, true } },
                { ExcludeSubfolders, { true, true, false } },
                { ExcludeFiles | ExcludeFolders, { false, false, false } },
                { ExcludeFiles | ExcludeSubfolders, { false, true, false } },
                { ExcludeFolders | ExcludeSubfolders, { true, false, false } },
                { ExcludeFiles | ExcludeFolders | ExcludeSubfolders, { false, false, false } },
            };
            const PCWSTR originals[] = { L"aaa_file.txt", L"aaa_folder", L"aaa_inner.txt" };
            const PCWSTR candidates[] = { L"zzz_file.txt", L"zzz_folder", L"zzz_inner.txt" };
            const bool folders[] = { false, true, false };
            const UINT depths[] = { 0, 0, 1 };

            for (const bool useBoost : { false, true })
            {
                for (const auto& combination : combinations)
                {
                    for (size_t i = 0; i < ARRAYSIZE(originals); ++i)
                    {
                        const std::wstring context = std::wstring(L"PLAN-01/") + originals[i] +
                                                     L"/flags=" + std::to_wstring(combination.flags) +
                                                     (useBoost ? L"/Boost" : L"/std");
                        auto engine = MakeEngine(useBoost, combination.flags, L"aaa", L"zzz");
                        auto item = MakeItem(originals[i], folders[i], depths[i]);
                        Assert::AreEqual(S_OK, item->PutNewName(L"stale-candidate"));
                        unsigned long index = 7;
                        const bool allowed = combination.allowed[i];
                        Assert::AreEqual(allowed, DoRename(engine, index, item), context.c_str());
                        Assert::AreEqual(allowed ? 8UL : 7UL, index, context.c_str());
                        AssertItem(item, combination.flags, allowed ? candidates[i] : nullptr, allowed ? Status::ShouldRename : Status::Init, allowed, context.c_str());
                    }
                }
            }
        }

        TEST_METHOD (EligibilityTracksCandidateStatusAndSelectionWithoutReplanning)
        {
            const Status statuses[] = {
                Status::Init, Status::ShouldRename, Status::ItemNameTooLong, Status::ItemNameInvalidChar, Status::ItemNameAlreadyExists
            };
            struct Candidate
            {
                PCWSTR name;
                bool eligible;
            };
            const Candidate candidates[] = {
                { L"done.txt", true }, { L"report.txt", false }, { L"", false }, { nullptr, false }
            };

            for (const auto status : statuses)
            {
                for (const auto& candidate : candidates)
                {
                    auto item = MakeItem(L"report.txt");
                    Assert::AreEqual(S_OK, item->PutNewName(candidate.name));
                    Assert::AreEqual(S_OK, item->PutStatus(status));
                    const bool expected = status == Status::ShouldRename && candidate.eligible;
                    AssertItem(item, 0, candidate.name, status, expected, L"PLAN-05/selected");
                    Assert::AreEqual(S_OK, item->PutSelected(false));
                    AssertItem(item, 0, candidate.name, status, false, L"PLAN-05/unselected");
                    Assert::AreEqual(S_OK, item->PutSelected(true));
                    AssertItem(item, 0, candidate.name, status, expected, L"PLAN-05/reselected");
                }
            }

            struct Exclusion
            {
                bool folder;
                UINT depth;
                DWORD excluded;
                DWORD allowed;
            };
            const Exclusion exclusions[] = {
                { false, 0, ExcludeFiles, ExcludeFolders | ExcludeSubfolders },
                { true, 0, ExcludeFolders, ExcludeFiles | ExcludeSubfolders },
                { false, 1, ExcludeSubfolders, ExcludeFolders },
                { true, 1, ExcludeSubfolders, ExcludeFiles },
            };
            for (const auto& test : exclusions)
            {
                auto item = MakeItem(L"report.txt", test.folder, test.depth);
                Assert::AreEqual(S_OK, item->PutNewName(L"done.txt"));
                Assert::AreEqual(S_OK, item->PutStatus(Status::ShouldRename));
                AssertItem(item, 0, L"done.txt", Status::ShouldRename, true, L"PLAN-05/before-exclusion");
                AssertItem(item, test.excluded, L"done.txt", Status::ShouldRename, false, L"PLAN-05/excluded");
                AssertItem(item, test.allowed, L"done.txt", Status::ShouldRename, true, L"PLAN-05/unrelated-exclusions");
            }
        }

        TEST_METHOD (InvalidCandidatesRemainVisibleButIneligible)
        {
            struct InvalidCase
            {
                PCWSTR id;
                PCWSTR replacement;
                Status status;
            };
            const InvalidCase cases[] = {
                { L"PLAN-06/invalid-character", L"bad<name", Status::ItemNameInvalidChar },
                { L"PLAN-06/too-long", L"abcdefghijklmnopqrst", Status::ItemNameTooLong },
            };
            // The long path is only fixture text; no directory or shell item is created.
            const std::wstring longPath = std::wstring(L"C:\\") + std::wstring(234, L'd') + L"\\aaa.txt";
            for (const bool useBoost : { false, true })
            {
                for (const auto& test : cases)
                {
                    auto engine = MakeEngine(useBoost, 0, L"aaa", test.replacement);
                    auto item = MakeItem(L"aaa.txt", false, 0, test.status == Status::ItemNameTooLong ? longPath.c_str() : nullptr);
                    unsigned long index = 0;
                    Assert::IsFalse(DoRename(engine, index, item), test.id);
                    Assert::AreEqual(1UL, index, test.id);
                    const std::wstring candidate = std::wstring(test.replacement) + L".txt";
                    AssertItem(item, 0, candidate.c_str(), test.status, false, test.id);
                }
            }
        }
    };
}

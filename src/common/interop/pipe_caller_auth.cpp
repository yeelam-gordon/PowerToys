#include "pipe_caller_auth.h"

#include <wincrypt.h>
#include <wintrust.h>
#include <softpub.h>
#include <imagehlp.h>

#include <cwctype>
#include <cstring>
#include <map>
#include <mutex>

#pragma comment(lib, "wintrust.lib")
#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "imagehlp.lib")

namespace interop_auth
{
    namespace
    {
        std::wstring ToLower(std::wstring s)
        {
            for (auto& c : s)
            {
                c = static_cast<wchar_t>(towlower(c));
            }
            return s;
        }

        std::wstring CanonicalizePath(const std::wstring& path)
        {
            // Backup semantics so this works for both files and directories.
            HANDLE h = CreateFileW(path.c_str(),
                                   0,
                                   FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                                   nullptr,
                                   OPEN_EXISTING,
                                   FILE_FLAG_BACKUP_SEMANTICS,
                                   nullptr);
            if (h == INVALID_HANDLE_VALUE)
            {
                // Fail closed: a path we cannot open/canonicalize must not slip past the
                // directory-prefix check as a non-canonical raw path.
                return {};
            }
            wchar_t buf[1024] = {};
            DWORD len = GetFinalPathNameByHandleW(h, buf, ARRAYSIZE(buf), FILE_NAME_NORMALIZED);
            CloseHandle(h);
            if (len == 0 || len >= ARRAYSIZE(buf))
            {
                return {};
            }
            std::wstring result(buf, len);
            if (result.rfind(L"\\\\?\\", 0) == 0)
            {
                result.erase(0, 4);
            }
            return result;
        }

        std::wstring BaseName(const std::wstring& path)
        {
            const auto pos = path.find_last_of(L"\\/");
            return pos == std::wstring::npos ? path : path.substr(pos + 1);
        }

        bool PathIsUnderDirectory(const std::wstring& canonicalFile, const std::wstring& directory)
        {
            if (directory.empty() || canonicalFile.empty())
            {
                return false;
            }
            std::wstring dir = ToLower(CanonicalizePath(directory));
            if (dir.empty())
            {
                // CanonicalizePath failed closed; an empty prefix must not match every path.
                return false;
            }
            if (!dir.empty() && dir.back() != L'\\')
            {
                dir.push_back(L'\\');
            }
            const std::wstring file = ToLower(canonicalFile);
            return file.size() > dir.size() && file.compare(0, dir.size(), dir) == 0;
        }

        bool BasenameAllowed(const std::wstring& canonicalFile, const std::vector<std::wstring>& allowed)
        {
            const std::wstring base = ToLower(BaseName(canonicalFile));
            for (const auto& a : allowed)
            {
                if (ToLower(a) == base)
                {
                    return true;
                }
            }
            return false;
        }

        bool LeafIsMicrosoft(PCCERT_CONTEXT cert)
        {
            if (!cert)
            {
                return false;
            }
            wchar_t name[256] = {};
            char organizationOid[] = szOID_ORGANIZATION_NAME;
            const DWORD n = CertGetNameStringW(cert, CERT_NAME_ATTR_TYPE, 0, organizationOid, name, ARRAYSIZE(name));
            return n > 1 && _wcsicmp(name, L"Microsoft Corporation") == 0;
        }

        // Anchor the signer's chain in the LOCAL MACHINE root store only (HCCE_LOCAL_MACHINE) rather than
        // WinVerifyTrust's default user+machine union. The Runner runs as the same user as a potential
        // attacker, so a user-writable CurrentUser\Root could otherwise forge a "Microsoft" signer; a
        // non-admin cannot plant a machine root, so this defeats the forge.
        bool ChainsToMachineRoot(PCCERT_CONTEXT leaf, HCERTSTORE additionalStore, FILETIME* verificationTime)
        {
            if (!leaf)
            {
                return false;
            }

            char codeSigningOid[] = szOID_PKIX_KP_CODE_SIGNING;
            LPSTR oids[] = { codeSigningOid };
            CERT_CHAIN_PARA para = {};
            para.cbSize = sizeof(para);
            para.RequestedUsage.dwType = USAGE_MATCH_TYPE_AND;
            para.RequestedUsage.Usage.cUsageIdentifier = 1;
            para.RequestedUsage.Usage.rgpszUsageIdentifier = oids;

            // Use cached revocation data only; the Authenticode chain policy below still applies.
            const DWORD flags = CERT_CHAIN_CACHE_ONLY_URL_RETRIEVAL |
                                CERT_CHAIN_REVOCATION_CHECK_CACHE_ONLY |
                                CERT_CHAIN_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT;

            PCCERT_CHAIN_CONTEXT chain = nullptr;
            if (!CertGetCertificateChain(HCCE_LOCAL_MACHINE, leaf, verificationTime, additionalStore, &para, flags, nullptr, &chain))
            {
                return false;
            }

            bool ok = false;
            const DWORD ignore = CERT_TRUST_REVOCATION_STATUS_UNKNOWN | CERT_TRUST_IS_OFFLINE_REVOCATION;
            if ((chain->TrustStatus.dwErrorStatus & ~ignore) == 0)
            {
                CERT_CHAIN_POLICY_PARA policyPara = {};
                policyPara.cbSize = sizeof(policyPara);
                CERT_CHAIN_POLICY_STATUS policyStatus = {};
                policyStatus.cbSize = sizeof(policyStatus);
                if (CertVerifyCertificateChainPolicy(CERT_CHAIN_POLICY_AUTHENTICODE, chain, &policyPara, &policyStatus))
                {
                    ok = (policyStatus.dwError == 0);
                }
            }

            CertFreeCertificateChain(chain);
            return ok;
        }

        bool VerifyMicrosoftSignedMachineRoot(const std::wstring& path, HANDLE file = nullptr)
        {
            WINTRUST_FILE_INFO fileInfo = {};
            fileInfo.cbStruct = sizeof(fileInfo);
            fileInfo.pcwszFilePath = path.c_str();
            fileInfo.hFile = file;

            WINTRUST_DATA wd = {};
            wd.cbStruct = sizeof(wd);
            wd.dwUIChoice = WTD_UI_NONE;
            wd.fdwRevocationChecks = WTD_REVOKE_NONE;
            wd.dwUnionChoice = WTD_CHOICE_FILE;
            wd.pFile = &fileInfo;
            wd.dwStateAction = WTD_STATEACTION_VERIFY;
            wd.dwProvFlags = WTD_SAFER_FLAG | WTD_CACHE_ONLY_URL_RETRIEVAL;

            GUID action = WINTRUST_ACTION_GENERIC_VERIFY_V2;
            HWND noWindow = static_cast<HWND>(INVALID_HANDLE_VALUE);
            const LONG status = WinVerifyTrust(noWindow, &action, &wd);

            bool result = false;
            if (status == ERROR_SUCCESS)
            {
                if (auto* provider = WTHelperProvDataFromStateData(wd.hWVTStateData))
                {
                    if (auto* signer = WTHelperGetProvSignerFromChain(provider, 0, FALSE, 0);
                        signer && signer->csCertChain != 0 && signer->pasCertChain[0].pCert)
                    {
                        const auto leaf = signer->pasCertChain[0].pCert;
                        result = LeafIsMicrosoft(leaf) && ChainsToMachineRoot(leaf, leaf->hCertStore, &signer->sftVerifyAsOf);
                    }
                }
            }

            wd.dwStateAction = WTD_STATEACTION_CLOSE;
            WinVerifyTrust(noWindow, &action, &wd);

            return result;
        }

        unsigned long long ReadModuleVersion(HMODULE module)
        {
            if (!module)
            {
                return 0;
            }
            unsigned long long result = 0;
            if (HRSRC resource = FindResourceW(module, MAKEINTRESOURCEW(1 /* VS_VERSION_INFO */), RT_VERSION))
            {
                if (HGLOBAL loaded = LoadResource(module, resource))
                {
                    const void* locked = LockResource(loaded);
                    const DWORD size = SizeofResource(module, resource);
                    if (locked && size >= sizeof(VS_FIXEDFILEINFO))
                    {
                        std::vector<BYTE> bytes(size);
                        memcpy(bytes.data(), locked, size);
                        for (size_t i = 0; i + sizeof(VS_FIXEDFILEINFO) <= bytes.size(); i += sizeof(DWORD))
                        {
                            VS_FIXEDFILEINFO version{};
                            memcpy(&version, &bytes[i], sizeof(version));
                            if (version.dwSignature == 0xFEEF04BD)
                            {
                                result = (static_cast<unsigned long long>(version.dwFileVersionMS) << 32) | version.dwFileVersionLS;
                                break;
                            }
                        }
                    }
                }
            }
            return result;
        }

        // --- Per-process verification cache -------------------------------------------------------
        // The cache itself is the header-only interop_auth::VerificationCache, owned per pipe server so
        // verdicts are physically partitioned by policy (key = pid + creation-time). Only the helper to
        // read a process's unforgeable creation-time key lives here.
        unsigned long long ProcessCreationKey(HANDLE process)
        {
            FILETIME create = {}, exit = {}, kernel = {}, user = {};
            if (!GetProcessTimes(process, &create, &exit, &kernel, &user))
            {
                return 0;
            }
            return (static_cast<unsigned long long>(create.dwHighDateTime) << 32) | create.dwLowDateTime;
        }
    }

    unsigned long long GetModuleVersion(const std::wstring& path)
    {
        if (path.empty())
        {
            return 0;
        }
        // Read the fixed file-version from the PE's RT_VERSION resource using kernel32-only APIs
        // (LoadLibraryEx as a data/resource image), avoiding the version.dll import lib.
        HMODULE mod = LoadLibraryExW(path.c_str(), nullptr, LOAD_LIBRARY_AS_IMAGE_RESOURCE | LOAD_LIBRARY_AS_DATAFILE);
        if (!mod)
        {
            return 0;
        }
        const auto result = ReadModuleVersion(mod);
        FreeLibrary(mod);
        return result;
    }

    unsigned long long GetOwnModuleVersion()
    {
        return ReadModuleVersion(GetModuleHandleW(nullptr));
    }

    HRESULT GetMappedImageSignaturePresence(void* image, bool& hasSignature)
    {
        if (!image)
        {
            return E_INVALIDARG;
        }
        static std::mutex imageMutex;
        std::scoped_lock lock(imageMutex);
        const auto* headers = ImageNtHeader(image);
        if (!headers)
        {
            return TRUST_E_SUBJECT_FORM_UNKNOWN;
        }
        IMAGE_DATA_DIRECTORY certificate{};
        if (headers->OptionalHeader.Magic == IMAGE_NT_OPTIONAL_HDR32_MAGIC)
        {
            const auto* optional = reinterpret_cast<const IMAGE_OPTIONAL_HEADER32*>(&headers->OptionalHeader);
            if (optional->NumberOfRvaAndSizes <= IMAGE_DIRECTORY_ENTRY_SECURITY)
            {
                return TRUST_E_SUBJECT_FORM_UNKNOWN;
            }
            certificate = optional->DataDirectory[IMAGE_DIRECTORY_ENTRY_SECURITY];
        }
        else if (headers->OptionalHeader.Magic == IMAGE_NT_OPTIONAL_HDR64_MAGIC)
        {
            const auto* optional = reinterpret_cast<const IMAGE_OPTIONAL_HEADER64*>(&headers->OptionalHeader);
            if (optional->NumberOfRvaAndSizes <= IMAGE_DIRECTORY_ENTRY_SECURITY)
            {
                return TRUST_E_SUBJECT_FORM_UNKNOWN;
            }
            certificate = optional->DataDirectory[IMAGE_DIRECTORY_ENTRY_SECURITY];
        }
        else
        {
            return TRUST_E_SUBJECT_FORM_UNKNOWN;
        }
        hasSignature = certificate.VirtualAddress != 0 || certificate.Size != 0;
        return S_OK;
    }

    HRESULT GetOwnModuleSignaturePresence(bool& hasSignature)
    {
        HMODULE module = GetModuleHandleW(nullptr);
        if (!module)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        return GetMappedImageSignaturePresence(module, hasSignature);
    }

    AuthResult VerifyMicrosoftSignedLaunchImage(HANDLE file,
                                               const std::wstring& path,
                                               unsigned long long expectedVersion)
    {
        AuthResult result;
        result.imagePath = path;
        if (!file || file == INVALID_HANDLE_VALUE)
        {
            result.reasonCode = L"image-handle-missing";
        }
        else if (expectedVersion == 0 || GetModuleVersion(path) != expectedVersion)
        {
            result.reasonCode = L"version-mismatch";
        }
        else if (!VerifyMicrosoftSignedMachineRoot(path, file))
        {
            result.reasonCode = L"not-microsoft-signed";
        }
        else
        {
            result.accepted = true;
        }
        return result;
    }

    AuthResult AuthenticateClient(HANDLE pipe, const CallerPolicy& policy, VerificationCache& cache)
    {
        AuthResult res;
        if (!policy.enabled)
        {
            res.accepted = true;
            return res;
        }

        ULONG pid = 0;
        if (!GetNamedPipeClientProcessId(pipe, &pid))
        {
            res.reasonCode = L"no-client-pid";
            return res;
        }
        res.pid = pid;

        if (policy.expectedClientPid.has_value() && policy.expectedClientPid.value() != pid)
        {
            res.reasonCode = L"pid-mismatch";
            if (policy.logReject)
            {
                policy.logReject(res);
            }
            return res;
        }

        // Hold the process handle for the whole check so the PID cannot be recycled under us.
        HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
        if (!process)
        {
            res.reasonCode = L"open-process-failed";
            if (policy.logReject)
            {
                policy.logReject(res);
            }
            return res;
        }

        const unsigned long long createTime = ProcessCreationKey(process);

        {
            AuthResult cached;
            if (cache.TryGet(pid, createTime, cached))
            {
                cached.pid = pid;
                CloseHandle(process);
                // Do not re-log on a cache hit (dedup across the per-message connections).
                return cached;
            }
        }

        wchar_t imageBuf[MAX_PATH * 2] = {};
        DWORD cch = ARRAYSIZE(imageBuf);
        std::wstring canonical;
        if (QueryFullProcessImageNameW(process, 0, imageBuf, &cch))
        {
            canonical = CanonicalizePath(imageBuf);
        }
        res.imagePath = canonical;

        const wchar_t* reason = L"";
        bool accepted = false;

        if (canonical.empty())
        {
            reason = L"image-path-failed";
        }
        else if (!PathIsUnderDirectory(canonical, policy.expectedDirectory))
        {
            reason = L"bad-directory";
        }
        else if (!BasenameAllowed(canonical, policy.allowedBasenames))
        {
            reason = L"bad-basename";
        }
        else if (policy.expectedVersion != 0 && GetModuleVersion(canonical) != policy.expectedVersion)
        {
            reason = L"version-mismatch";
        }
        else
        {
            bool signatureOk = true;
            if (policy.requireMicrosoftSignature)
            {
                bool serverHasSignature = true;
                if (FAILED(GetOwnModuleSignaturePresence(serverHasSignature)))
                {
                    signatureOk = false;
                    reason = L"server-signature-check-failed";
                }
                else if (serverHasSignature)
                {
                    signatureOk = VerifyMicrosoftSignedMachineRoot(canonical);
                }
            }
            if (!signatureOk)
            {
                if (*reason == L'\0')
                {
                    reason = L"not-microsoft-signed";
                }
            }
            else
            {
                accepted = true;
            }
        }

        CloseHandle(process);

        res.accepted = accepted;
        res.reasonCode = reason;

        cache.Put(pid, createTime, res);

        // Required rejection logging (once per process instance, deduped by the cache above).
        if (!accepted && policy.logReject)
        {
            policy.logReject(res);
        }

        return res;
    }
}

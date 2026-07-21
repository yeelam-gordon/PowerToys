CULPRIT_FILES: src/modules/FileLocksmith/FileLocksmithLib/IPC.cpp

CULPRIT_FUNCTIONS: Writer::start, Writer::add_path

FIX: The Writer class opens an ofstream in text mode (default) but writes binary Unicode data (WCHAR) to it. Text mode performs line-ending conversions which corrupts Unicode paths. The fix is to open the stream in binary mode by passing std::ios::binary flag to the ofstream constructor.

CITED_FIX_PR: none

CONFIDENCE: high

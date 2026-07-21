CULPRIT_FILES: src/modules/MouseWithoutBorders/App/Core/Encryption.cs
CULPRIT_FUNCTIONS: GetEncryptedStream, GetDecryptedStream, GenLegalIV, GenLegalKey (uses cached key derived with fixed salt)
FIX: Both encryption/decryption functions use a fixed IV from InitialIV constant and a cached key derived with the same fixed salt, violating cryptographic best practices. The fix generates a random 16-byte salt and 16-byte IV per connection, sends them as a 32-byte cleartext header via ExchangeEncryptionHeader, derives the key fresh with PBKDF2 using the random salt, and removes the cached key dictionary.
CITED_FIX_PR: #48742
CONFIDENCE: high
USED_SKILL: Security regression playbook from mousewithoutborders-knowledge identified PR #48742 for per-connection random salt+IV (MSRC 118042), confirmed worktree uses fixed InitialIV constant and cached LegalKeyDictionary.

CULPRIT_FILES: src/modules/MouseWithoutBorders/App/Core/Encryption.cs
CULPRIT_FUNCTIONS: GenLegalIV(), InitEncryption(), GetEncryptedStream(), GetDecryptedStream()
FIX: Replace the static InitialIV constant with per-connection random salt and IV generation. Generate a unique random IV for each connection using symAl.GenerateIV(), exchange it during connection handshake, and derive per-connection keys using connection-specific salts instead of the static InitialIV string.
CITED_FIX_PR: none
CONFIDENCE: high

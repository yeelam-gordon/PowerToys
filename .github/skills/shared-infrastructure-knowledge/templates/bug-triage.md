# Shared Infrastructure Bug Triage

1. Identify the failing module and the shared contract it consumes.
2. Reproduce through a second consumer when possible to distinguish module logic from shared logic.
3. Search every caller, serializer, and side file before choosing the fix location.
4. Check upgrade state, resource lookup failure, policy-disabled launch, and shutdown paths.
5. Fix at the narrowest shared layer that preserves existing consumers; add consumer-level tests.

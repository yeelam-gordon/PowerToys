# Shared Infrastructure PR Review Checklist

- [ ] Read the diff before loading module/shared playbooks.
- [ ] Enumerate all callers and serialized readers/writers of the changed contract.
- [ ] Identify module, Settings UI, and test consumers.
- [ ] Check old persisted files, missing new fields, migration, and side-file consistency.
- [ ] When the changed WinUI window matches the demonstrated localized native-title/custom-title-bar
      path, require a non-empty fallback before deferred layout can consume it; do not generalize
      the rule to unrelated windows.
- [ ] For separate processes, check policy at every entry point required by the module's verified
      contract, plus reverse-order cleanup; do not invent an executable-side gate.
- [ ] Run the smallest tests for every affected consumer, not only the declaring project.
- [ ] Mark aspirational rules and known current violations honestly.

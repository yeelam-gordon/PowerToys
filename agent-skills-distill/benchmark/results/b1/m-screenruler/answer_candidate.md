CULPRIT_FILES: MeasureToolCore/ToolState.h

CULPRIT_FUNCTIONS: CommonState::cursorPosSystemSpace (field), Core::MouseCaptureThread (writes the field)

FIX: The cursorPosSystemSpace field is written via InterlockedExchange64 which requires 8-byte alignment, but POINT structures are typically 4-byte aligned by default. On some CPUs, accessing under-aligned memory with 64-bit interlocked operations causes undefined behavior and crashes. The fix is to add alignas(8) to the cursorPosSystemSpace field declaration in CommonState.

CITED_FIX_PR: #41556

CONFIDENCE: high

USED_SKILL: The skill's regression playbook identified the exact issue as "Cross-thread cursor read misalignment" with PR #41556, pointing to ToolState.h cursorPosSystemSpace needing alignas(8) and static_assert checks for safe 64-bit interlocked operations.

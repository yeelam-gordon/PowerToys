CULPRIT_FILES: src/modules/awake/Awake/Core/Manager.cs
CULPRIT_FUNCTIONS: SetTimedKeepAwake
FIX: The Subscribe call uses a discard lambda `_ => HandleTimerCompletion("timed")` as the second parameter, which binds to the Subscribe(onNext, onError) overload instead of the onCompleted overload. When the Observable completes after countdown, the completion handler never runs and the mode stays stuck in TIMED. The fix changes `_ =>` to `() =>` to properly bind to the onCompleted parameter.
CITED_FIX_PR: #43785
CONFIDENCE: high
USED_SKILL: Regression catalog R2 described the exact discard-lambda bug where the onCompleted handler was mistakenly routed to the onError overload, preventing proper expiration.

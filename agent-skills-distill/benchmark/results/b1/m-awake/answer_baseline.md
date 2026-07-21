CULPRIT_FILES: src/modules/awake/Awake/Core/Manager.cs
CULPRIT_FUNCTIONS: SetTimedKeepAwake
FIX: The Observable.Interval chain at line 346 uses TakeWhile with remaining.TotalSeconds > 0, which stops the sequence when time reaches 0 but doesn't guarantee the completion handler (HandleTimerCompletion) executes. The condition should be >= 0, or the logic should ensure the completion handler is called when the timer actually expires, not just when the sequence stops.
CITED_FIX_PR: none
CONFIDENCE: high

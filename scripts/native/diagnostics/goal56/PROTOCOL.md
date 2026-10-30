# Goal #56: fixed High Contrast comparison protocol

Protocol established before execution on 30 October 2026.

Compare develop `86d60322f6f1f1dec06974bf5f9e1738a3bb80c7` with PR #69 `c85a51118ac0f175cb21c79c3f7503ee1bc8ecd0`.

Four independent disposable Windows jobs: two repetitions per revision. Each job uses separate clean source checkouts, .NET SDK 10.0.403, locked restore, the original High Contrast activation, original fixtures and original acceptance limits. Run the unchanged session and Creation UI checks once. Then run a separately built diagnostic copy once with a new fixture/state directory. Retain every outcome and original screenshot. A failed unchanged run must not prevent the diagnostic run. No further repetitions are authorised by this protocol.

The diagnostic copy changes only the validation client: observe editor identity before Save; immediately before each SetValue read ValuePattern.IsReadOnly once, call SetValue once, and record its result. On failure observe current editor identity, focused element, foreground HWND, native window responsiveness, enabled/read-only state and supported patterns. Diagnostic observation errors are recorded separately and cannot turn a functional failure into a pass. No focus action, delay, retry or acceptance change is added. Additional observations can affect timing, so diagnostic passes do not replace unchanged failures. Native WM_NULL responsiveness is not a claim of dispatcher queue idleness.

Product source, installer, privacy controls, protected CI requirements and Continuous 250 ms remain unchanged. This experiment does not sign, install or publish packages.

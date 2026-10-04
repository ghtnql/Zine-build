# Observed release verification — 2026-10-04

Unity 2022.3.62f3, iOS target, real OriginalScene play mode, game-view render texture1600×739. Manager inspected actual PNGs: original perspective/material game, readable Korean ranking, ten records reachable by scrolling. Ranking opener moved from overlapping Time HUD to bottom safe-area center and verified again.

Integration checks PASS: forward movement, left/right turns through existing mobile callbacks, ranking pauses gameplay, input cancel restores saved name, top10 content/scroll, closing resumes, game-over run values, Back closes, zero runtime errors. Editor networking disabled; displayed ranking rows are synthetic fixtures, not production data.

Backend11 unittest PASS; public HTTPS synthetic-player registration and own deletion PASS. Actual privacy page downloaded and compared against source PASS. Physical iPhone keyboard, safe-area hardware, audio output, background interruptions and real native networking remain unverified. Server has no audio output device, so no sound-quality claim is made.

Muse actual assignments: native UI3 bounded attempts with partial edits; Snake2 attempts then success; client1 successful; backend1 successful; initial review1 successful. gpt-6.1-sol medium completed only unresolved native UI/layout, plus one opener positioning follow-up. Manager integrated and verified. Usage not reported by Muse; no savings/cost estimate.

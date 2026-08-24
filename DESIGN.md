# Holo interface direction

Holo should feel like a quiet macOS instrument: spatial, precise, and calm. It should not look like a generic analytics dashboard or a science-fiction control panel.

References were reviewed on July 16, 2026.

## Reference lessons

- Apple places Liquid Glass in the functional layer—navigation and controls—and recommends standard materials for content. Use system components first and custom glass sparingly. See [Materials](https://developer.apple.com/design/human-interface-guidelines/materials) and [Designing for macOS](https://developer.apple.com/design/human-interface-guidelines/designing-for-macos/).
- Linear's 2026 refresh says interface elements should not compete for attention they have not earned, and that structure should be felt rather than seen. Holo therefore uses fewer icons, separators, borders, and elevated containers. See [A calmer interface for a product in motion](https://linear.app/now/behind-the-latest-design-refresh).
- Things succeeds through a stable sidebar, a single obvious working area, progressive disclosure, and purposeful motion. Holo keeps advanced sensing and rejection controls out of the primary path. See [Things](https://culturedcode.com/things/).
- Raycast's 2026 Mac redesign uses Liquid Glass in ways that support the function of a launcher while staying familiar and compact. Holo follows the same rule for its toolbar and primary controls. See [The New Raycast](https://www.raycast.com/blog/the-new-raycast).
- Apple's recognition of iA Writer and Mela reinforces task focus, contextual dimming, and attention to interaction details over decoration. See the [2025 Apple Design Awards](https://developer.apple.com/design/awards/2025/).
- The community-maintained [Slopless design directory](https://www.slopless.design/) is useful as a pattern audit, not a design authority: it identifies the repeated gradients, inflated cards, interchangeable dashboards, and filler copy that make generated interfaces converge on the same look.

## Anti-slop rules

1. No purple–cyan gradients, neon glow, decorative blobs, or forced dark mode.
2. No bento grid of interchangeable metric cards.
3. No boxes inside boxes. A container must communicate real grouping or interaction.
4. No uppercase eyebrow copy, excessive tracking, or oversized marketing headings inside the app.
5. One semantic accent color. Status colors are reserved for success, warning, recording, and error.
6. No decorative icon tiles. Symbols identify actions or objects only.
7. No invented metrics or ornamental charts. Every number must come from real capture or evaluation data.
8. Liquid Glass is for toolbar and important controls. Content uses standard system materials.
9. Prefer native `NavigationSplitView`, `List`, `Form`, `Table`, `LabeledContent`, `Gauge`, `ProgressView`, menus, sheets, and toolbars.
10. Motion explains a state change; nothing continuously pulses merely to look alive.

## Signal-processing lessons

- [Acustico](https://www.cs.dartmouth.edu/~hci/papers/Acustico.pdf) detects a surface tap as a short power pulse whose neighboring windows are substantially quieter. Holo applies the same temporal principle after capture, with deliberately looser thresholds because it has only the MacBook microphone rather than contact accelerometers.
- Essentia's [EffectiveDuration](https://essentia.upf.edu/reference/std_EffectiveDuration.html) descriptor explicitly distinguishes percussive from sustained sounds using time above 40% of the envelope maximum. Holo combines that cue with late-to-impact energy and early-energy concentration; no single cue rejects an event by itself.
- Research on [on-device mechano-acoustic touch classification](https://doi.org/10.3390/app11114834) supports combining temporal and spectral features with a learned classifier. Holo uses ten spatially varied captures per broad zone and a regularized linear boundary, while retaining nearest-example novelty and user-recorded negative checks.

## Screen hierarchy

- **Desk:** two continuous two-zone rails around one MacBook silhouette, one compact result strip, and no-profile onboarding in place. Individual zones are not rendered as four floating cards.
- **Calibration:** one target at a time. Beginning setup is the explicit capture intent; Holo then collects ten clean taps spread across the highlighted area, disarms during each move, and automatically arms the next zone after a short transition. A visible Arm control remains available after a pause. A measured consistency check can redo the weakest zone before saving, and Talking rejection capture is surfaced as the recommended final step.
- **Actions:** a four-row editor grouped by the left and right side of the MacBook. Native pickers progressively reveal only the fields needed for the selected action, including Shortcuts, app/file bookmarks, shell commands, and screenshots. Combos are a fourth section holding only the two-tap gestures the user has explicitly added, so a profile that uses none carries no extra rows; a combo reuses the same row editor as a zone rather than introducing a second one, and the sixteen possible pairs are never rendered as a grid. A zone that begins a combo says so on its own row, because that zone is where the added wait is actually paid. The time allowed between two taps is a single labelled slider inside that same section rather than a field on each combo or a separate preferences window: it governs every combo, so it is shown once, next to what it governs.
- **Desk while a combo is pending:** a tap held to see whether a second tap follows uses the same subdued secondary highlight that guided capture uses for its current target, not the accent fill reserved for an accepted tap. The distinction is a state change the user can act on, not decoration.
- **Diagnostics:** factual hardware and signal details in a form, with sensing comparisons disclosed on demand.
- **Accuracy Test:** a focused 60-tap run followed by a plain results table and four-by-four confusion matrix.

The sidebar keeps Desk, Calibration, Actions, and Accuracy Test in workflow order. Hardware and sensing diagnostics live in a separate Advanced section.

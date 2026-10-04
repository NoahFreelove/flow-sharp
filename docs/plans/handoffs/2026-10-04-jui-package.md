# JUI package integration — 2026-10-04

The owner supplied `../jui/build/packages/Jui.0.10.0.nupkg` and confirmed JUI is
complete. Its integration guide reports M1–M7 complete. C# binding availability
is no longer a prerequisite blocking the native DAW. Backend readiness remains
required before frontend implementation.

## Inspected artifact

- Package `Jui` version `0.10.0`, `lib/net10.0/Jui.dll`, packaged
  `runtimes/linux-x64/native/libjui.so`, no managed dependencies.
- SHA-256: `4bf4d91e60fa18b44b9c4d3e0973ed4e3c336bc43ce418f45b1756b2d6dc8b84`.
- Package repository commit: `57d22957f892d8b4597bbd761a3964e499c718b5`.
- A temporary consumer restored from the local feed, verified native version
  `0.10.0`, created a `JuiContext`, ran a headless frame and disposed it successfully.
  Evidence: `/tmp/flow-jui-package-smoke-run.log`. It uses `UseAppHost=false` to
  avoid downloading an unnecessary executable-host package from a remote feed.
- This proves packaged managed/native loading on this host, not window/input/GL
  integration, visual fidelity, or distribution compatibility. Package requires
  system FreeType and has no older-distribution baseline guarantee.

## DAW mapping

Use the C# NuGet API. Retain the established GLFW/OpenGL host plan; Vulkan is also
available but does not require changing the selected renderer. The application
owns the window, rendering context, clock and threads. JUI owners are created,
used and explicitly disposed on the UI thread. Borrowed draw data expires at the
next frame. No JUI API belongs in the audio callback.

- Knob/fader/ranged slider return changed values and a widget ID; `Active(id)` is
  available for drag lifecycle. Adapt a drag to begin/update/commit backend
  parameter preview. Wheel, keyboard and reset gestures also need explicit
  commit grouping; a value-change flag alone does not identify gesture completion.
- Convert catalog values to the appropriate JUI ranges; audio-taper display must
  retain the backend's linear gain values.
- Feed coherent backend meter snapshots into JUI's dB meters; JUI supplies visual
  ballistics and clip indication. Retain the last good snapshot on read contention.
- Canvas and scroll/list APIs support piano-roll/arrangement rendering and hit
  testing; document edits remain backend actions. JUI does not own musical data.
- Multiline `JuiDocument`/editor and app-supplied syntax spans support Flow source
  editing. Its local text undo is distinct from accepted project-action history.
- Beat-clock/reactivity consumes host transport/meter data, never controls audio
  scheduling. Background builds marshal results to the same control owner that
  runs the host poll loop.

Source references inspected: sibling `docs/integration.md`, C# `JuiContext`,
`JuiContext.Knob`, `JuiContext.Fader`, `JuiContext.Meter`, `JuiContext.Music`, and
package README/nuspec. No sibling source or package was modified.

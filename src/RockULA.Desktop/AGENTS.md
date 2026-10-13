# Desktop host instructions
Root AGENTS applies. Read contracts.md, timing.md and ADR 0014.
PlayerSession is the only live machine owner. UI commands await acknowledgement;
input is an immutable coalesced snapshot; rendered frames contain no live RAM/CPU.
Do not call RunFrame or edit guest state from an Avalonia event or audio callback.
SDL owns audio/game joystick handles, never the Avalonia window. Joystick enumeration
and polling stay on the UI thread; audio queue/reset/close access is serialized.
Stop the owner before closing audio handles. Late UI input during shutdown is harmless.
Native device failure must permit silent keyboard playback with a visible explanation.
Test control/ownership failures with fake output, native ABI with dummy audio and
virtual joystick smoke, and declare real physical-device acceptance separately.
Keep host dependencies in Desktop; Core and Formats never import SDL or Avalonia.

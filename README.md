# Song Survival MVP

This repository now contains two directions for the same concept:

- `Unity prototype` for native/mobile experimentation
- `Web MVP` in `web/` for link-based testing and Vercel deployment

## What is included

- Runtime bootstrap that creates the game even from an empty scene
- Assembly definition split for runtime and editor code
- Microphone permission and capture flow
- Calibration/readiness check with fallback messaging
- Audio feature extraction for energy, bass, brightness, peaks, and confidence
- One-touch-drag player controller
- Three hazard families driven by the analyzed audio
- Local best-score persistence

## Open and run

1. Open this folder as a Unity project in `2022.3 LTS` or newer.
2. Create an empty scene if Unity does not create one automatically.
3. Press Play. The bootstrapper will create camera, UI, player, and hazard systems at runtime.
4. On device, enable microphone permission and test with music from the same device speaker first. If readiness is weak, use the built-in retry flow or move to an external speaker.

## Web MVP

The deployable browser version lives in `web/`.

### Local development

1. `cd web`
2. `npm install`
3. `npm run dev`
4. Open the shown local URL in a browser. For iPhone, use Safari when testing on-device.

### Vercel deployment

This repo includes [vercel.json](C:\Users\111\codexxxxx\music\vercel.json) so Vercel can build the `web/` app directly from the repository root.

Expected flow:

1. Import the GitHub repo into Vercel
2. Keep the detected root at the repository root
3. Deploy
4. Open the production link on iPhone Safari and allow microphone access

Recommended phone test:

1. Open the Vercel link in `Safari`
2. Put the song on speaker, not headphones
3. Tap `Enable microphone`
4. Calibrate for a few seconds
5. Start the run

## Run tests

1. Open Unity Test Runner.
2. Run the `EditMode` suite in `Assets/Tests/EditMode`.
3. Verify the scoring, difficulty, and audio-analysis tests pass before device testing.

## Important notes

- Same-device playback and microphone capture can be inconsistent on iPhone. The prototype includes a calibration gate and fallback messaging, but it cannot bypass OS audio constraints.
- There are no scenes, art assets, or prefabs checked in. The prototype builds the full MVP from code to keep the greenfield setup small.
- iOS builds include an editor post-process step that writes `NSMicrophoneUsageDescription` into `Info.plist`.
- Common Unity cache/build folders are ignored via `.gitignore`.

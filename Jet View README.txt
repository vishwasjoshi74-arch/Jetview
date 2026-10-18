JETVIEW
=================
An interactive 3D jet engine viewer built in Unity. The user can orbit
around the engine, start and stop it with realistic sound, take it apart
into its individual components, and read details about each part.

Live demo (WebGL): https://vizzyyy-01.itch.io/jet-view
GitHub: https://github.com/vishwasjoshi74-arch/Jetview
Author: Vishwas Joshi


1. SETUP INSTRUCTIONS
---------------------
Requirements
  - Unity Hub and Unity 6000.3
  - (For the web build only) the "WebGL Build Support" module

Open the project
  1. Clone or download this repository.
  2. Open Unity Hub, click Add > Add project from disk, and select the
     repository folder (the one containing Assets, Packages and
     ProjectSettings).
  3. Open the project. Unity regenerates the Library folder by itself,
     which can take a few minutes the first time.
  4. Open the main scene from the Assets folder and press Play.

Scene configuration (if you rebuild the scene)
  1. Main Camera: add OrbitAroundModel. Drag the engine root object into
     "Target".
  2. Create an empty object "EngineController" and add EngineSpin and
     EnginePartSeparator.
  3. In EngineSpin, drag the blade meshes into the Fan, Compressor and
     Turbine lists, and assign the Start, Running and Stop sounds.
  4. In EnginePartSeparator, add each part to the Parts list and fill in
     its Label, Details and Distance.
  5. Create an empty object "UIManager" and add EngineUI.

Important import settings
  - Engine model: Import Settings > Model > Meshes > tick Read/Write
    (needed for click-to-select in builds).
  - Audio files: Load Type "Compressed In Memory", format Vorbis.

Build for WebGL (itch.io)
  1. File > Build Profiles > Web > Switch Platform.
  2. Player Settings > Publishing Settings > Compression Format: Gzip,
     tick Decompression Fallback (or choose Disabled).
  3. Build into an empty folder.
  4. Zip the CONTENTS of the build folder as a .zip so that index.html
     is at the root of the archive.
  5. On itch.io create an HTML project, upload the zip and tick
     "This file will be played in the browser".


2. THIRD-PARTY PACKAGES AND ASSETS USED
---------------------------------------
Unity packages (all official Unity packages):
  - TextMeshPro (part of Unity UI, com.unity.ugui): all on-screen text.
  - Unity UI (uGUI): canvas, buttons, slider and details box.
  - Input System (com.unity.inputsystem): optional. The scripts work with
    both the old Input Manager and the new Input System.

No other external code libraries or plugins are used. All scripts were
written for this project.

Assets:
  - 3D jet engine model: https://www.innerscene.com/tools/library/3d-parts/turbofan-quarter-cutaway-e0184203
  - Engine sound: https://pixabay.com/sound-effects/search/jet-engine/
  - Skybox: https://assetstore.unity.com/packages/2d/textures-materials/sky/allsky-free-10-sky-skybox-set-146014


3. KEY FEATURES
---------------
Camera and navigation
  - Slow automatic orbit around the engine, paused while the user
    controls the camera and resumed after a short delay.
  - Hold the right mouse button and drag to rotate freely.
  - Mouse wheel and an on-screen slider to zoom, kept in sync.
  - Smoothed camera motion, similar to a Sketchfab model viewer.

Part focus
  - A / D or Left / Right arrows: focus the previous / next part.
  - Left-click a part to focus it.
  - Space: return to the whole engine.
  - The camera frames each part automatically using its real bounds, so
    it works even when model pivots are at 0,0,0.
  - The name of the focused part is shown at the top left.

Part information
  - An "i" button under the part name opens a details box.
  - Name and description of each part are typed in the Inspector
    (Label and Details fields), so no code changes are needed.
  - Box width is adjustable; height fits the text automatically or can
    be set manually.

Engine simulation
  - Start / Stop button (green when stopped, red when running).
  - Fan, compressor and turbine blades rotate clockwise around the
    local Z axis at different speeds.
  - Each blade rotates around the centre of its own geometry, which
    solves the off-centre pivot problem.
  - Three sounds (start, running loop, stop). Blade speed is tied to
    the length of the start and stop sounds, and sound and blades stay
    in sync even if the engine is stopped part-way through starting.
  - Volume control in the Inspector.

Disassembly
  - "Disassemble Parts" / "Assemble Parts" button separates 13-15 parts
    along the engine axis with smooth animation.
  - The engine keeps running while disassembled.
  - Distance and offset of every part can be adjusted in the Inspector
    to avoid overlap.

Interface and delivery
  - All UI is built at runtime from code with TextMeshPro text.
  - Works on desktop and as a WebGL build hosted on itch.io.


4. SCRIPTS
----------
  OrbitAroundModel     Camera orbit, zoom, focus, part name, info box
  EngineSpin           Blade rotation and three-stage engine sound
  EnginePartSeparator  Part list, labels, details, disassembly
  EngineUI             Start/Stop and Disassemble/Assemble buttons


5. CONTROLS SUMMARY
-------------------
  Right mouse + drag   Rotate
  Mouse wheel          Zoom
  A / D or arrows      Previous / next part
  Left click on part   Focus part
  Space                Whole engine
  "i" button           Show / hide part details

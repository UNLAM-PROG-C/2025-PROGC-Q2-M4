# Opening & Building the Tetris VS Project in Unity

This guide explains (1) how to open the project correctly, (2) how to configure scenes and settings, and (3) how to build playable binaries for different platforms using the Unity Editor GUI. It also includes troubleshooting steps.

---

## 1. Prerequisites

| Item | Details |
|------|---------|
| Unity Version | Use the exact version listed in `TP-Integrador/TetrisVS/ProjectSettings/ProjectVersion.txt`. (Open the file in a text editor to confirm.) |
| Disk Space | Ensure you have enough space for the Library folder and build output. |
| Permissions | You must have read/write permissions to the repository directory. |
| OS-Specific Tooling | For Windows builds on non-Windows systems, consider using Unity’s cross-platform build support modules. |

If you installed a different Unity version than the project’s, the editor will prompt you to upgrade; avoid upgrading unless the team agrees, because version changes can cause asset re-import or incompatibilities.

---

## 2. Opening the Project via Unity Hub

1. Open Unity Hub.
2. Click the “Open” (or “Add”) button.
3. Navigate to:  
   `.../<repo-root>/TP-Integrador/TetrisVS/`
4. Select that folder (the one containing `Assets`, `Packages`, `ProjectSettings`).
5. Wait for Unity to import assets (first open may take several minutes).
6. If error dialogs appear about missing modules (e.g., Windows Build Support), install those modules through Unity Hub.

---

## 3. Verifying Project Integrity

After the editor loads:

1. In the `Project` window, confirm you see folders: `Assets/Scripts`, `Assets/Editor` (if created), etc.
2. Open `Console` window (Window → General → Console) and check for:
   - `Board Awake:` logs (they indicate Tetris block initialization).
   - Missing script or shader errors (fix before building).
3. If you see errors about `TetrisBlocks` array being null:
   - Open the scene containing the Board (likely something like `Main` or `Game` in `Assets/Scenes`).
   - Select the `Board` GameObject and ensure the `TetrisBlocks` array is populated in the Inspector with Tile references.

---

## 4. Configuring Scenes for Build

1. Go to File → Build Settings.
2. In the Build Settings window:
   - Click “Add Open Scenes” to include the currently open scene.
   - Ensure at least one gameplay scene (e.g., `Game.unity`) is checked.
3. Order matters for initial load: the scene at index 0 will be the first loaded when the game starts.

If no scene is checked or added, the build will fail.

---

## 5. Player Settings (Optional Adjustments)

Open: Edit → Project Settings → Player.

Recommended tweaks:
- Product Name: Set to `TetrisVS`.
- Company Name: Your organization or leave default.
- Icon / Splash (optional).
- Resolution and Presentation: Adjust default screen size and fullscreen options.

Ensure scripting backend (IL2CPP vs Mono) matches platform requirements (IL2CPP required for certain console builds).

---

## 6. Simple GUI Build (No Scripts)

1. File → Build Settings.
2. Select target platform:
   - Windows: “PC, Mac & Linux Standalone” + Target Platform “Windows” + Architecture “x86_64”.
   - Linux: Same group, choose “Linux” + Architecture “x86_64”.
   - macOS: Choose “MacOS”.
3. (If module missing) Click “Install with Unity Hub”.
4. Click “Build” (or “Build and Run”).
5. Choose an output directory (e.g., `Builds/Windows/`).
6. Let the process finish; watch the progress bar.

Result:
- Windows: `.exe` plus `*_Data` folder.
- Linux: Executable file + Data folder.
- macOS: `.app` bundle.

---

## 7. Multiplayer Manual Test (Local)

Since this is a socket-based host/client setup:

1. Build (or run in Editor) one instance as Host:
   - In Unity play mode (or the built executable), trigger the `HostGame()` action (button or UI tied to `MultiplayerManager`).
2. On the same machine, run a second instance:
   - Use `JoinGame("127.0.0.1")` or a UI input field for IP.
3. Verify:
   - The host logs: “Client handshake received”.
   - The client receives initial board state / queue sync.
   - Active piece movement on the client sends `player_action` messages to server (watch Console logs).

If you are using two builds on the same machine, ensure no port conflicts and firewall allowances on port `7777` (default).

---

## 8. Using the Editor Build Script (If Added)

If you placed the `BuildAutomation.cs` inside `Assets/Editor/`:

Command-line example (Windows):
```
"C:\Path\To\Unity\Editor\Unity.exe" -batchmode -nographics -quit ^
 -projectPath "C:\Path\To\Repo\TP-Integrador\TetrisVS" ^
 -executeMethod BuildAutomation.PerformBuild ^
 -customBuildTarget=Win64 -customBuildName=TetrisVS -customBuildPath=Builds
```

Check output in:
`TP-Integrador/TetrisVS/Builds/StandaloneWindows64/`

If `BuildAutomation.PerformBuild` isn’t found:
- Confirm file path and name.
- Ensure it has `public static void PerformBuild()` and is inside Editor folder.
- Remove namespaces or include them properly in `-executeMethod Namespace.Class.Method`.

## 9. Where Build Artifacts Should Live

Recommended structure after several builds:
```
TP-Integrador/
  TetrisVS/
  Builds/
    StandaloneWindows64/
    TetrisVS.exe
    TetrisVS_Data/
    StandaloneLinux64/
    TetrisVS
    TetrisVS_Data/
    StandaloneOSX/
    TetrisVS.app
```

If you used custom path flags: `-customBuildPath=Builds/Manual`, adjust accordingly.


## 10. Fast Checklist (Copy/Paste)

```
[ ] Correct Unity version installed
[ ] Project opened from TP-Integrador/TetrisVS
[ ] No console compile errors
[ ] At least one scene added & checked in Build Settings
[ ] TetrisBlocks array populated (Board component)
[ ] MultiplayerManager present in scene (or added)
[ ] HostGame() works (logs server listening)
[ ] JoinGame("127.0.0.1") from second instance connects
[ ] Build produced expected executable in Builds/
```
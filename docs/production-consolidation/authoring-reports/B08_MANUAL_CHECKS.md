# B08 — player observations

**October 11, 2026: functional player checks accepted by the user.** Settings persistence across relaunch, Stop/Start, Restart, menu navigation and scene switching are closed. The steps below remain the historical checklist, not a request to repeat accepted checks. Artistic approval and the historical toe observation remain separate. Next: [author a fresh scene](../../../CONTENT_AUTHORING_WALKTHROUGH.md).

Use the fresh Windows player at:
C:/Users/artwh/OneDrive/Documents/DazPoseWizard/validation/DazPoseUnityValidation/Builds/Authoring/DazPoseGame.exe

1. In Main Menu, open Settings and close it. If you want to change audio, choose your levels; quit/relaunch and check they persist. Connection is explicit. Toy commands have no receipt/error reaction; troubleshooting is in Intiface.
2. Select Invitation. Let its roughly 100-second sequence finish, checking speech, framing, smooth pose transitions, wardrobe, palm contact/motion and dissolve. Its host should show Completed.
3. Start again and press Stop during active motion; it should show Stopped. Start resumes a new sequence using current tuning. Restart reloads the saved scene.
4. Return to Main Menu and select Encore. Check walking, seating/standing, action, aura/spell and the final dissolve. Return to the menu.
5. Record actual player navigation/persistence success separately from appearance approval. The historical second-outfit toe observation is still a separate pending visual check.

Automated evidence already covers bounded contracts, exact saved-scene Editor playback and saved UI callback navigation. Actual player clicks are not inferred from these results.

Desktop automation limitation: the installed computer-use skill directs loading its guidance, which requires: “Use `node_repl` JavaScript for all Computer Use actions.” This session exposes neither that node_repl tool nor enabled native CUA APIs. No substitute SendInput/UIAutomation or custom player-control protocol was used. The fresh executable can be launched and its interactive session/window checked; the remaining player interaction is human observation.

Skill: C:/Users/artwh/.codex/plugins/cache/openai-bundled/computer-use/26.1002.52244/skills/computer-use/SKILL.md

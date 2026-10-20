# Gate B - exact manual directions

The source is ready. Art operates Unity; native acceptance is pending. This temporary scene is only for protected-card return, not for adding outfits/hair.

1. Let Unity finish compiling in `validation/DazPoseUnityValidation`. Open **DazPose > Wardrobe > Appearance Delivery**.
2. Click **Create / open Gate B scene**. Accept a save prompt if you want to save your current scene. The result should open `Assets/__WardrobeProtectedCards/Scenes/GateB_ProtectedCards.unity`. Do not select anything in the hierarchy.
3. Press **Play**. In the Game view, find **Card Return Gate**. Click **Run all Gate B technical checks** once. This serially runs the labelled cases; wait for Finished. It deliberately includes labelled Stop/fault cases. Those must report PASS as expected terminal cases.
4. Watch the ordinary direct-card case: entrance holds two seconds; outgoing changes hold two seconds; Lara returns to the entrance look/pose/place and camera returns. The no-change case should stay still. A standalone sequence should keep its changes; a sequence inside a card should return. Technical checks also cover direct/explicit/drawn/nested routes, seated/hidden entrances, parent motion, faults and Stop checkpoints.
5. After the suite finishes, select **Standing** and **Fixed (pitch + roll)**. Click **Run direct card**. During its labelled **Outgoing** hold (after Lara has changed), click **Stop current run**. This records the additional interactive-stop case. It should stop without completing the normal return/trailing cue.
6. Click **Write report**, then exit Play. Report location: `docs/wardrobe-directing/evidence/gate-b-native.json`. Reply **B PASS** if the ordinary returns looked correct and report is PASS, or give the failed case label and what happened. No screenshots needed.

All managed checks passing does not establish native success. W08 starts after Art accepts Gate B. The fixture scene and one-use setup/runner will be retired after acceptance; production wardrobe assets/GUIDs remain.

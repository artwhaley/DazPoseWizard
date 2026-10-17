# First Contact D8 oracle

The user's complete source below is retained substantially verbatim, including its original notation, punctuation and performance cues. The revised execution stack settles the implementation semantics and normalized source spelling. Both files are authoritative; the original is not a representative example.

```text
Scene Setup:
	Lara starts hidden at lara_start transform
	Playerbody starts hidden at player_start transform
	Scene is the testing void
	Camera starts on camera_start transform
	Lara in Idle Animation


Intro Card:
	Await Lara.DissolveIn(lara_start transform)
	Camera.track(lara)
	Await Lara.Say("Hi there.  I was hoping you were brave enough to meet me." 
	Await Lara.LocomoteTo(stepforward transform)
	Await Lara.PersistantPose(SexyVamp)
	Await Lara.Say("I'm Lara.  I'm a digital hallucination.")  
	Await Lara.MagicShader(glowy Wireframe mode)
	Wait 500ms
	Await Lara.MagicShader(Regular shader)
	"Await Lara.Say("Well, more than a hallucination.  A bit of a...  projection.  An intrusion? (giggles) I like that one.  Intrusion.")
	Await Lara.DissolveTo(Lounger)
	Await Lara.Say("I'm the most fun you've ever had.  But I come with... conditions.")
	Await Lara.PersistentPose(Leaning Forward)
	Await Lara.Say("I can only give you pleasure if I have the power to do so.  The more you give up to me, the more I can do for you.  Let's see what you're giving up, tonight."
	Await Popup.show(kinks selection window)  (this is a full screen window where the player can select all of their interests, that will be used by the card selection portions of the session to tailor it to the individual player.  )
	Await Lara.Say("Not bad for a start.")
	Calculate.SubmissionScore(This is a calculation we'll do based on how many of the kinks the player enables.)
	Await Lara.Say("This is your submission score.  The more activities you consent to, the higher it goes, but... I don't want you bending past your real limits.  Honesty is important.   But...(giggles)  so's letting me try new things.  So next time we start a scene...  look for things you're willing to try for me and let me know if you come up with any."
	Lara.PersistentPose(CrossedArmsAmused)
	Await Lara.Say("Now let's look at your equipment...(giggles)"Not that equipment, yet.")
	Await Popup.Show(equipment selection window - full screen window where the player can edit the equipment)  If the player has ANY equipment available, when they close the equipment window we will do a random selection of one item they own and a new popup will show that explains "Needed for this session" and lists the item.   Other scenes may ask for 2 items, or up to 3 items with internal tags 'anal' etc.  this concept will develop.  But for this scene, we pick one item and tell the player what it is.  
	Await Lara.Say("And finally, tell me what you're wearing....")
	Popup.show(clothing dialog) this is a simple dialog for now that just has choices for 'shoes, socks, shirt, pants, bra, underwear' with checkboxes.  The user selects what they're wearing.  If the user is wearing nothing, then we get: 
		Lara.Say("Now that's a go-getter.  The less you wear to our sessions, the better.")
		We add 5 points to the previous neutral at 50 happiness score.  
	if the user marked one or two items of clothing
		Lara.Say("I like you naked, but I can wait to get you there..."
		No change to happiness score.
	if the user marked more than two items of clothing:
		Lara.PlayOneshotgesture(dissproval)
		Lara.Say("That's way too dressed.  The fewer you wear the happier I am.  In fact...  take one off now.")
		Text on screen tells them to remove the least risque of the clothing items.  Internal mental status updates to track the new state. 

Warmup Phase:
	Lara.SitAt(lounger transform)
	Lara.PersistentSitPose(Lounging)
	Lara.Say("Alright, let's get you warmed up.")
	Loop until completion => 40%:
		Draw card with tag Warmup and execute it
	If articles of clothing > 0:
		Lara.Say("I think it's time to lose the rest of the clothes...  do that and tell me when you're back.  ((UI prompt, waits until the player clicks 'ready')
	Loop until completion => 100%:
		Draw card with tag Warmup and execute it

Main Phase:
	Lara.DissolveTo(MainPosition1)
	Lara.persistentpose(Main1)
	Lara.Say("That was all the warmup I'm going to give you.  Now let's get down to business.")
	Loop until completion => 30%:
		Draw card with tag Main and tag Standing and execute it
	Lara.DissolveTo(Lounger, in sit position - new feature!)
	Draw Card with Tags matching the equipment items selected in the intro scene and execute it.
	Executor.BonusIntensityModifier = 1.2
	Loop until completion => 60%:
		Draw card with tag Main and tag Sitting and execute it with 120% intensity
	Executor.BonusIntensityModifier = 1.4
	Loop until completion => 100%:
		Draw card with tag Main and tag Sitting and execute it with 140% intensity
	
Climax Phase:
	Playerbody.dissolveIn()
	Lara.DissolveTo(Bedside)
	Lara.Say("I'm amused enough, I suppose.  And since this is your first time, I'm not going to frustrate you... or even make you beg.   I'm just going to make you cum.")
	Lara.Grip(Penis)
	Motiondriver.PlayFunscript(Finisher1)
	Wait for either end of funscript, or player to press the "I came" button that automatically advances to the next phase.  
	If end of fun script, motiondriver.PlayFunscript(FinishLoop, Looping) - 
	Now we loop until the player clicks the "I came" button
	
Resolution Phase:
	Lara.Say("*giggles) Well, you seem to understand the assignment.  Some times you're going to have to work for it.   Some times you may have to work and not even get it then.  It all depends on my mood, and how much you decide to trust me."
	Lara.DissolveTo(start location)
	Lara.PersistentPose(Sassy)
	Lara.Say(“Now, how am I going to keep you thinking about me until our next session?”)
	Director.Draw and Execute Card (Resolution)
	Lara.Say(“Alright, that’s our session for tonight.  Until next time…  behave.  Or don’t.   I get off either way. “Giggles”  Bye!)
```

## Traceability

Line numbers refer to the original source block above. Each meaningful original line is retained below; grouped continuation lines share their section's ticket. Status begins as planned and must not imply playback acceptance.

| Oracle line | Implementation ticket | Status | Notes |
| --- | --- | --- | --- |
| 1 | D5.5-11; D8-01 | source implemented; content/playback pending | Scene Setup: |
| 2 | D5.5-11; D8-01 | source implemented; content/playback pending | Lara starts hidden at lara_start transform |
| 3 | D5.5-11; D8-01 | source implemented; content/playback pending | Playerbody starts hidden at player_start transform |
| 4 | D5.5-11; D8-01 | source implemented; content/playback pending | Scene is the testing void |
| 5 | D5.5-11; D8-01 | source implemented; content/playback pending | Camera starts on camera_start transform |
| 6 | D5.5-11; D8-01 | source implemented; content/playback pending | Lara in Idle Animation |
| 9 | D5.5-12..14 | source implemented; content/playback pending | Intro Card: |
| 10 | D5.5-12..14 | source implemented; content/playback pending | Await Lara.DissolveIn(lara_start transform) |
| 11 | D5.5-12..14 | source implemented; content/playback pending | Camera.track(lara) |
| 12 | D5.5-12..14; D5.5-08..09; D8-10 | source implemented; content/playback pending | Await Lara.Say("Hi there.  I was hoping you were brave enough to meet me." |
| 13 | D5.5-12..14 | source implemented; content/playback pending | Await Lara.LocomoteTo(stepforward transform) |
| 14 | D5.5-12..14; D5.5-15 | source implemented; content/playback pending | Await Lara.PersistantPose(SexyVamp) |
| 15 | D5.5-12..14; D5.5-08..09; D8-10 | source implemented; content/playback pending | Await Lara.Say("I'm Lara.  I'm a digital hallucination.") |
| 16 | D5.5-10 | source implemented; content/playback pending | Await Lara.MagicShader(glowy Wireframe mode) |
| 17 | D5.5-12..14 | source implemented; content/playback pending | Wait 500ms |
| 18 | D5.5-10 | source implemented; content/playback pending | Await Lara.MagicShader(Regular shader) |
| 19 | D5.5-12..14; D5.5-08..09; D8-10 | source implemented; content/playback pending | "Await Lara.Say("Well, more than a hallucination.  A bit of a...  projection.  An intrusion? (giggles) I like that one.  Intrusion.") |
| 20 | D5.5-12..14 | source implemented; content/playback pending | Await Lara.DissolveTo(Lounger) |
| 21 | D5.5-12..14; D5.5-08..09; D8-10 | source implemented; content/playback pending | Await Lara.Say("I'm the most fun you've ever had.  But I come with... conditions.") |
| 22 | D5.5-12..14; D5.5-15 | source implemented; content/playback pending | Await Lara.PersistentPose(Leaning Forward) |
| 23 | D5.5-12..14; D5.5-08..09; D8-10 | source implemented; content/playback pending | Await Lara.Say("I can only give you pleasure if I have the power to do so.  The more you give up to me, the more I can do for you.  Let's see what you're giving up, tonight." |
| 24 | D5.5-12..14 | source implemented; content/playback pending | Await Popup.show(kinks selection window)  (this is a full screen window where the player can select all of their interests, that will be used by the card selection portions of the session to tailor it to the individual player.  ) |
| 25 | D5.5-12..14; D5.5-08..09; D8-10 | source implemented; content/playback pending | Await Lara.Say("Not bad for a start.") |
| 26 | D5.5-12..14 | source implemented; content/playback pending | Calculate.SubmissionScore(This is a calculation we'll do based on how many of the kinks the player enables.) |
| 27 | D5.5-12..14; D5.5-08..09; D8-10 | source implemented; content/playback pending | Await Lara.Say("This is your submission score.  The more activities you consent to, the higher it goes, but... I don't want you bending past your real limits.  Honesty is important.   But...(giggles)  so's letting me try new things.  So next time we start a scene...  look for things you're willing to try for me and let me know if you come up with any." |
| 28 | D5.5-12..14; D5.5-15 | source implemented; content/playback pending | Lara.PersistentPose(CrossedArmsAmused) |
| 29 | D5.5-12..14; D5.5-08..09; D8-10 | source implemented; content/playback pending | Await Lara.Say("Now let's look at your equipment...(giggles)"Not that equipment, yet.") |
| 30 | D5.5-12..14 | source implemented; content/playback pending | Await Popup.Show(equipment selection window - full screen window where the player can edit the equipment)  If the player has ANY equipment available, when they close the equipment window we will do a random selection of one item they own and a new popup will show that explains "Needed for this session" and lists the item.   Other scenes may ask for 2 items, or up to 3 items with internal tags 'anal' etc.  this concept will develop.  But for this scene, we pick one item and tell the player what it is. |
| 31 | D5.5-12..14; D5.5-08..09; D8-10 | source implemented; content/playback pending | Await Lara.Say("And finally, tell me what you're wearing....") |
| 32 | D5.5-12..14 | source implemented; content/playback pending | Popup.show(clothing dialog) this is a simple dialog for now that just has choices for 'shoes, socks, shirt, pants, bra, underwear' with checkboxes.  The user selects what they're wearing.  If the user is wearing nothing, then we get: |
| 33 | D5.5-12..14; D5.5-08..09; D8-10 | source implemented; content/playback pending | Lara.Say("Now that's a go-getter.  The less you wear to our sessions, the better.") |
| 34 | D5.5-12..14 | source implemented; content/playback pending | We add 5 points to the previous neutral at 50 happiness score. |
| 35 | D5.5-12..14 | source implemented; content/playback pending | if the user marked one or two items of clothing |
| 36 | D5.5-12..14; D5.5-08..09; D8-10 | source implemented; content/playback pending | Lara.Say("I like you naked, but I can wait to get you there..." |
| 37 | D5.5-12..14 | source implemented; content/playback pending | No change to happiness score. |
| 38 | D5.5-12..14 | source implemented; content/playback pending | if the user marked more than two items of clothing: |
| 39 | D5.5-12..14 | source implemented; content/playback pending | Lara.PlayOneshotgesture(dissproval) |
| 40 | D5.5-12..14; D5.5-08..09; D8-10 | source implemented; content/playback pending | Lara.Say("That's way too dressed.  The fewer you wear the happier I am.  In fact...  take one off now.") |
| 41 | D5.5-12..14 | source implemented; content/playback pending | Text on screen tells them to remove the least risque of the clothing items.  Internal mental status updates to track the new state. |
| 43 | D6; D7-07..08 | planned | Warmup Phase: |
| 44 | D6; D7-07..08 | planned | Lara.SitAt(lounger transform) |
| 45 | D6; D7-07..08; D5.5-15 | planned | Lara.PersistentSitPose(Lounging) |
| 46 | D6; D7-07..08; D5.5-08..09; D8-10 | planned | Lara.Say("Alright, let's get you warmed up.") |
| 47 | D6; D7-07..08 | planned | Loop until completion => 40%: |
| 48 | D6; D7-07..08 | planned | Draw card with tag Warmup and execute it |
| 49 | D6; D7-07..08 | planned | If articles of clothing > 0: |
| 50 | D6; D7-07..08; D5.5-08..09; D8-10 | planned | Lara.Say("I think it's time to lose the rest of the clothes...  do that and tell me when you're back.  ((UI prompt, waits until the player clicks 'ready') |
| 51 | D6; D7-07..08 | planned | Loop until completion => 100%: |
| 52 | D6; D7-07..08 | planned | Draw card with tag Warmup and execute it |
| 54 | D7-09..10 | planned | Main Phase: |
| 55 | D7-09..10 | planned | Lara.DissolveTo(MainPosition1) |
| 56 | D7-09..10; D5.5-15 | planned | Lara.persistentpose(Main1) |
| 57 | D7-09..10; D5.5-08..09; D8-10 | planned | Lara.Say("That was all the warmup I'm going to give you.  Now let's get down to business.") |
| 58 | D7-09..10 | planned | Loop until completion => 30%: |
| 59 | D7-09..10 | planned | Draw card with tag Main and tag Standing and execute it |
| 60 | D7-09..10 | planned | Lara.DissolveTo(Lounger, in sit position - new feature!) |
| 61 | D7-09..10 | planned | Draw Card with Tags matching the equipment items selected in the intro scene and execute it. |
| 62 | D7-09..10 | planned | Executor.BonusIntensityModifier = 1.2 |
| 63 | D7-09..10 | planned | Loop until completion => 60%: |
| 64 | D7-09..10 | planned | Draw card with tag Main and tag Sitting and execute it with 120% intensity |
| 65 | D7-09..10 | planned | Executor.BonusIntensityModifier = 1.4 |
| 66 | D7-09..10 | planned | Loop until completion => 100%: |
| 67 | D7-09..10 | planned | Draw card with tag Main and tag Sitting and execute it with 140% intensity |
| 69 | D8-03..07 | planned | Climax Phase: |
| 70 | D8-03..07 | planned | Playerbody.dissolveIn() |
| 71 | D8-03..07 | planned | Lara.DissolveTo(Bedside) |
| 72 | D8-03..07; D5.5-08..09; D8-10 | planned | Lara.Say("I'm amused enough, I suppose.  And since this is your first time, I'm not going to frustrate you... or even make you beg.   I'm just going to make you cum.") |
| 73 | D8-03..07 | planned | Lara.Grip(Penis) |
| 74 | D8-03..07 | planned | Motiondriver.PlayFunscript(Finisher1) |
| 75 | D8-03..07 | planned | Wait for either end of funscript, or player to press the "I came" button that automatically advances to the next phase. |
| 76 | D8-03..07 | planned | If end of fun script, motiondriver.PlayFunscript(FinishLoop, Looping) - |
| 77 | D8-03..07 | planned | Now we loop until the player clicks the "I came" button |
| 79 | D8-08..09 | planned | Resolution Phase: |
| 80 | D8-08..09; D5.5-08..09; D8-10 | planned | Lara.Say("*giggles) Well, you seem to understand the assignment.  Some times you're going to have to work for it.   Some times you may have to work and not even get it then.  It all depends on my mood, and how much you decide to trust me." |
| 81 | D8-08..09 | planned | Lara.DissolveTo(start location) |
| 82 | D8-08..09; D5.5-15 | planned | Lara.PersistentPose(Sassy) |
| 83 | D8-08..09; D5.5-08..09; D8-10 | planned | Lara.Say(“Now, how am I going to keep you thinking about me until our next session?”) |
| 84 | D8-08..09 | planned | Director.Draw and Execute Card (Resolution) |
| 85 | D8-08..09; D5.5-08..09; D8-10 | planned | Lara.Say(“Alright, that’s our session for tonight.  Until next time…  behave.  Or don’t.   I get off either way. “Giggles”  Bye!) |

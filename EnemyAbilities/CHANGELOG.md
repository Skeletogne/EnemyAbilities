# Changelog

## 1.6.0
- Gave Larvae a new ability: Caustic Pod!
- You can no longer play as the bison boulder by picking one up as Drifter.
- Removed Xi Construct "prioritises players" config option as it hasn't worked since 1.4.0.
- Updated Xi Construct core projectile visuals slightly. Still not exact, but closer!
- Added MiscFixes dependency.
- Moved the changelog.

## 1.5.2
- Fixed a frequent Clay Apothecary NRE that I somehow missed
- Changed the visual charge effect for the Clay Apothecary Tar Ball to something less noisy

## 1.5.1
- LOTSA BUG FIXES!
- Solved a whole pile of NREs
- Solus Transporters should no longer permanently lose collision after using Fling.
- Bison Rocks should no longer get stuck in a weird rolling state. They're still a bit buggy tho lmao
- Clay Templar grenades should clip terrain mid-flight a little less often.
- Fixed some networking issues with Clay Apothecary tar blobs.
- Lunar Golem fire trails should no longer randomly disappear before exploding.
- Removed "KABOOM!" log spam from Laser Sweep. Oops.
- Xi Constructs should no longer rarely get stuck in their recall state indefinitely.
- Vultures should no longer commit to swoop if their target is above them.

## 1.5.0
- Gave Lunar Golems a new ability: Laser Sweep!
- Fixed an NRE related to tractor-beamed enemies getting killed during the fling wind-up.
- Removed an Error Log message from the TractorBeam module that should've really been a Debug message anyway.

## 1.4.0
- Gave Clay Apothecaries a new ability: Tar Deluge!
- Should no longer replace Pillar of Blood visuals with Grovetender chains.
- Prospectors should no longer get yoinked out of the ground by Transport Drones.
- Fixed an NRE related to Prospectors getting stuck underground.
- Xi Constructs previously had trouble making up their minds about their targets. This has a band-aid fix: Xi Constructs will now ALWAYS target players. 

## 1.3.2
- Updated Grovetender ghost visuals to look fiery to differentiate from Happiest Mask visuals.

## 1.3.1
- The Grovetender ghost item (should) no longer appear as a white item.
- Added DamageSources to each of the mod's attacks.

## 1.3.0
- Gave Grovetenders a new Special: Mass Resurrect!
- Added some new visuals for Solus Prospectors' Drill Burrow
- Minor touchups to the Bison Rock ability but it's still kinda buggy.

## 1.2.0
- Gave Bighorn Bison a new Secondary: Unearth Boulder!
- Networked the mod's various projectiles and attacks.
- Added a boatload of Configuration options. (Some of them are even untested!)
- Added R2API DamageType dependency.

## 1.1.1
- Local idiot forgets to update the README properly
- Got rid of the accursed extra apostrophe in the manifest description

## 1.1.0
- Gave Xi Constructs a new Secondary: Core Launch!
- Clay Templars should now correctly use Grenade Barrage within 10 seconds of last seeing a target, rather than after.
- (hopefully) solved some NREs here and there.

## 1.0.1
- Updated manifest to include git repository link. that's it

## 1.0.0
- Exists
# Changelog

# 1.11.2
- Added a config option for bison boulders for adjusting the spread angle.
- Minor VFX update to Bison mini-boulders. (they start at a random rotation now, that's it)
- Added extra sounds and visual cues to Clay Apothecary Tar Deluge
- Added a trail effect to enemies thrown with Solus Transporters' tractor beam.
- Made the Lunar Golem Laser Sweep blue. (now with extra sound and visual cues!)
- Made the Greater Wisp Inferno Wheel green. (now also with extra sound and visual cues!)
- Reduced default Caustic Pod explosion radius (12m => 8m)

## 1.11.1
- Xi Constructs no longer disable their main hurtbox during core launch. A new config option has been added to toggle this.
- Alpha Constructs can no longer use Alpha Tripwire from inside Drifter's bag.
- Alpha Tripwire no longer activates if the two Alpha Constructs are within 3m of eachother to prevent really small, hard-to-see tripwires.
- Bison can no longer unearth a boulder within 5m of another boulder.
- Changed this a few versions ago but forgot to include it and can't remember which version it was, so here it is - Clay Templars now have better VFX on their grenades.

## 1.11.0
- Gave Stone Titans a new ability: Shockwave Stomp!
- Changed the Bison Rock spawn sound to make it less ridiculously loud.
- Fixed a Swoop NRE related to them losing their target during windup.
- Reduced the default base damage of Alpha Tripwire (350% => 250%)
- Increased the speed of Grovetender gravestone projectiles (60 => 120)
- Lowered the base health of Greater Wisp inferno wheel projectiles (130 +39 per level => 100 +30 per level)

## 1.10.1
- Fixed the Root and Slow configs for Alpha Constructs 

## 1.10.0
- Gave Alpha Constructs a new ability: Alpha Tripwire!
- Greater Wisp Fire Carousel fireballs now correctly scale max health with level.
- Clay Templars have less endlag on their attacks, which should encourage them to use Cluster Grenade more often.
- Solus Transporters can no longer pick up and throw Scorch Worms.

## 1.9.0
- Gave Solus Extractors a new ability: Tendril Tether!
- Tightened the spread on Bison Boulders if they fire more than 1 from 30 degrees to 15 degrees.
- Bison Boulders no longer target neutral character bodies.
- Gave Xi Core a pulsing visual effect upon getting embedded so it's easier to locate.

## 1.8.0
- Reworked the Bison Boulder ability to make it less janky! Can be damaged by both players and enemies, launches three mini-boulders on death and is killed instantly by Bison melee attacks.
- As a result, changed a few of the Bison config options.
- Actually added the .dll this time lmao
- Lots of under-the-hood changes to make the mod less boilerplate-y and easier for myself to make future additions to the mod.
- In particular finally made use of the BaseModule class and dechunkified the PluginConfig. With that in mind, I HIGHLY recommend clearing your Config file for this update!
- Changed Greater Wisps' Inferno Wheel from a secondary to a special ability.
- Lunar Golems will no longer use Laser Sweep on airborne targets.
- Solus Prospectors will no longer use Drill Burrow on airborne targets.

## 1.7.0
- Gave Greater Wisps a new ability: Inferno Wheel!
- Added LanguageAPI dependency.
- Added name tokens for the mod's CharacterBody projectiles. (Xi Core shows up as Xi Construct, as they share hitboxes - will look into this at some point).

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
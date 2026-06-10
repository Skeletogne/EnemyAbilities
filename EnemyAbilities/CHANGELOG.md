# Changelog

# 1.16.0
- Gave Childs a new ability: Spatial Mischief!
- Greater Wisp inferno wheel fireballs now reset their progress upon the Greater Wisp being stunned.

# 1.15.5
- Greater Wisp inferno wheel fireballs will now correctly update max health to reflect their config values.
- Added some more guards to the Brass Contraption ability code to prevent NRE spam.

# 1.15.4
- Addressed a bug that causes Brass Contraption projectiles to expand infinitely. Was unable to reproduce this bug on my end, but added some guards that should prevent this behaviour.

# 1.15.3
- Added a load of icons made by DTEE to the mod!
- Made another attempt to correctly network Angler Eyes. We'll see if this one sticks.

# 1.15.2
- Beetles now correctly update their stacks of Strength in Numbers to 0 if they're the only beetle nearby.
- Beetle summon animation duration lasts longer and they now can be interrupted by taking damage.
- Beetles have additional restrictions on their summon. They have to be more than 40m (adjustable) away from any target, rather than their current target. This should reduce instances of them summoning despite being near the player due to targeting a far-away enemy. They also cannot summon if they have been damaged in the last 10 seconds.
- Bison rocks now deal 2.5x damage when detonated by a player, allowing it to be used more effectively as a counterattack. Added a configuration option to tune this multiplier.
- Added config settings for on-kill proc chance for Acid pods. By default these are set to 0.

# 1.15.1
- Beetles now ACTUALLY retain 100% of their gold and exp reward, and summoned Beetles half.
- Fixed a bug that stopped Solus Extractors from using Tendril Tether
- Solus Scorchers' spawn states now last 1 second instead of 0.

# 1.15.0
- Gave Solus Scorchers a new ability: Jet Boost!
- Void Anglers now provide vision for their owner Void Reavers, allowing the Reavers to use their primary without direct line of sight.
- Void Anglers default range reduced (500m => 200m). Attack charge time increased (1.5s => 3s). Add some sound cues and visual touch-ups to their attacks.
- Summoning Beetles now retain 100% of their gold and exp reward, and summoned Beetles inherit half of that value.
- Strength in Numbers got a new custom buff icon courtesy of Synodii!
- Solus Extractors now actually require line of sight to use Tendril Tether.
- Solus Extractors now unhook early if their continued trajectory would take them away from their target. 
- Reduced the volume of most of the mod's sounds.

# 1.14.0
- Gave Beetles a new special ability: Summon Reinforcements!
- Void Anglers should now correctly appear on clients in multiplayer.
- Buffed Solus Extractor tether's projectile speed from 100 to 120.
- Gave Solus Prospectors some new burrowing SFX.

# 1.13.0
- Gave Brass Contraptions a new special ability: Explosive Toll!
- Larva caustic pod explosion range now correctly reads the right config option. Default explosion radius restored to 12m.
- Added a sound cue to the start of Alloy Vultures' swoop attacks.
- Polished up the README to make additional module changes more clear.
- Added additional config options to modify the base stats and director cost of each enemy tweaked in the mod. 
- Added SoundAPI dependency.

# 1.12.1
- Changed Gravekeeper Ghosts to be even more red to distinguish them from burning enemies.
- Fixed the Angler Eye Beam Cooldown config option being locked at the lowest setting.
- Solved a few NREs here and there.
- Slightly reduced the distance that non-oiled players can be from an oil blob before a Solus Scorcher prioritises attacking the blob. 

# 1.12.0
- Gave Void Reavers a new ability: Summon Void Anglers!
- Gave Solus Scorchers smarter AI!

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
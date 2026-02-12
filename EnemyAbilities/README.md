# Enemy Abilities

This is a difficulty mod that increases the challenge of defeating the game's various enemies by giving them new skills!
Most abilities increase the difficulty of fighting a given enemy, however some abilities will open up new counter strategies! 

## Alloy Vulture Secondary: Swoop

- Swoops towards the target, slashing with it's talons.
- Inflicts Bleed
- Will stun itself if it flies into a wall!

## Bighorn Bison Secondary: Unearth Boulder

- Unearths a boulder in front of it.
- If the boulder is hit by a Bison's melee attack it is launched towards nearby targets!
- Can be launched by any attack that deals more than 1000% damage! (watch out for Jellyfish!)

## Blind Pest Secondary: Thwomp Stomp

- Slams down with a poison explosion to hit targets directly beneath it.
- Enters a grounded state for three seconds afterwards.

## Clay Apothecary Special: Tar Deluge

- Spawns a Tar Ball that it charges for 3 seconds. If it takes damage during the charge, the Tar Ball will get bigger and deal more damage!
- Creates a large lingering tar area upon landing. Deals low damage, but applies tar. The zone is larger the more charged the Tar Ball was!
- If killed during the charge, it blows up, damaging nearby monsters!
- Only usable below 60% health.

## Clay Templar Utility: Grenade Barrage

- Launches a volley of five tar-filled grenades over cover to flush it's target out.
- Only usable if it doesn't have line of sight, and can draw an arc to the target.

## Grovetender Special: Mass Resurrect

- Grovetenders passively collect gravestones when nearby enemies die.
- They then launch collected gravestones at the player, which explode into the ghost of the killed enemy.
- Ghosts provide armour to the grovetender.
- Only usable below 50% HP.

## Lunar Chimera (Golem) Special: Laser Sweep (new!)

- Sweeps a laser along the ground towards the player, igniting the ground.
- Ignited ground deals light AoE, and explodes after a short delay.
- Only usable below 90% HP. Has 2 charges by default. 

## Solus Prospector Secondary: Drill Burrow

- Burrows beneath the ground, then emerges beneath it's target shortly after.

## Solus Transporter Utility: Tractor Beam / Fling

- Picks up monsters with a tractor beam, before tossing them at its target.
- Deals damage dependent on the flung monster's weight.

## Xi Construct Secondary: Core Launch
- Spins up to fire it's core at the nearest player.
- The core embeds in the ground for a few seconds, and transfers any damage it takes to the Xi Construct.

# Notes

- All modules are enabled by default! Don't like a skill? You can disable it in the config!
- May contain bugs! Please report any that you find :)

# Credits

- .score for the PluginConfig and for some AISkillDriver utils that I filched from Enemies++
- rune580 for RiskOfOptions

# Changelog

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




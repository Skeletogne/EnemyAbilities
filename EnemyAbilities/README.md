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

## Clay Templar Utility: Grenade Barrage

- Launches a volley of five tar-filled grenades over cover to flush it's target out.
- Only usable if it doesn't have line of sight, and can draw an arc to the target.

## Grovetender Special: Mass Resurrect (new!)

- Grovetenders passively collect gravestones when nearby enemies die.
- They then launch collected gravestones at the player, which explode into the ghost of the killed enemy.
- Ghosts provide armour to the grovetender.
- Only usable below 50% HP.

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




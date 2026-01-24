# Enemy Abilities

Now with Networking AND Configuration Options!
Adds the following:

## Alloy Vulture Secondary: Swoop

- Swoops towards the target, slashing with it's talons.
- Inflicts Bleed
- Will stun itself if it flies into a wall!

## Bighorn Bison Secondary: Unearth Boulder (new!)

- Unearths a boulder in front of it.
- If the boulder is hit by a Bison's melee attack it is launched towards nearby targets!
- Can be launched by any attack that deals more than 1000% damage! (watch out for Jellyfish!)

## Blind Pest Secondary: Thwomp Stomp

- Slams down with a poison explosion to hit targets directly beneath it.
- Enters a grounded state for three seconds afterwards.

## Clay Templar Utility: Grenade Barrage

- Launches a volley of five tar-filled grenades over cover to flush it's target out.
- Only usable if it doesn't have line of sight, and can draw an arc to the target.

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




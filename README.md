# Murmurations

Murmurations is an experimental visual mod for **The Long Dark** that adds large, dynamic flocks of crows moving together in the sky (called Murmurations).

The project is primarily a technical and visual demonstration rather than a real gameplay mod. It was made to experiment with large groups of independently moving birds and to see how convincing murmuration-like behavior could look inside The Long Dark.

## Experimental Status

This mod is **experimental** and may cause performance issues depending on your hardware and the settings used. Large numbers of visible or simulated birds can be demanding, so performance may vary significantly from one machine to another.

An optional automatic spawning system is included, allowing murmurations to appear naturally while playing. This makes it possible to use the mod as part of a normal game, but that is not really its primary purpose.

Most aspects of the murmurations can be adjusted through ModSettings.

I don't plan on developing this mod any further, I'll fix whatever needs fixing if major issues pop but it most likely won't go beyond that.

(And no, shooting at a murmuration will not make birds fall from the sky. The birds are purely visual and are not actual wildlife entities.)

## Installation

1. Install MelonLoader.
2. Install [ModSettings](https://github.com/DigitalzombieTLD/ModSettings/).
3. Place `Murmurations.dll` inside your `Mods` folder.

### Flock sound (optional)

Murmurations can play the sound of the flock, coming from the birds nearest to you and fading out with distance.

1. Install [AudioManager](https://github.com/DigitalzombieTLD/AudioManager). Without it the murmurations are silent.
2. Place `Sound_of_massed_starlings.wav` (from the `Murmurations/Sounds` folder) inside `Mods/Murmurations/`.

## Credits

The flock sound is ["Starling Roost, Ham Wall"](https://www.richardmudhar.com/blog/2019/11/the-sound-of-starlings-massing/) by Enquiring Ear (recorded 6 November 2019), licensed under [Creative Commons Attribution 4.0 (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/). It was converted from MP3 to a mono 32 kHz WAV, normalised, and its end was crossfaded into its start so it loops seamlessly.

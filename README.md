# Explanation
This repository contains a game made for the practical course `Virtual Reality with Unity` part of the Ludwig-Maximilans-Universität (LMU) media informatics masters programm.

# Requirements
This game was build using Unity 6000.0.49f1. For the practical it was necessary to incorporate the following aspects:
 - Asymmetric multiplayer (using the fusion2 sdk)
 - Handtracking
 - UI elements
 - An input method other than controllers (extra hardware)
 - Haptic feedback
 - Virtual Reality 
The theme of the game revolved around hollywood/future hacking and the Meta Quest 3 was used for development and playing the game.

# The MVP, 404: Not Found
In this game two players are trying to escape a prison together, where guards roam and doors block their way. One player is the hacker, who build a hacking device which allows him to interact with his environment. This device was built using an ESP32. It had rotary encoders and switches as an input and connects via blueetooth to the device running the game. It was worn on the forearm. The other player was the sneaker, who uses his keen senses to avoid detection and steal from guards who roam the area. This player had a device strapped on his leg, which contained an accelerometer via which sneaking/walking was detectable and send over a server to the game. Locomotion in the game was done via a one to one mapping from movement in the real world to the game world or via teleportation. Certain parts of the game are not traversable through teleportation, so the sneaking person had to sneak into these and when not sneaking triggers a vibration on the device strapped to his legs. Together they have to use the information each of them have to escape through the levels and try not to get detected.

# Controls 
- Teleport using the a button.
- Push buttons by using the virtual hands to push them down.
- Grab objects (keycards) by holding the secondary trigger (on the quest located where the middle finger would grab the controller).
- Hacker only: Select mirrors using the a button and rotate them using the knobs of the hardware device (if not available use the arrow keys on a keyboard).
- Sneaker only: Walk across marked floors to reach certain parts of the level.

# Run the game
The folders `Assets`, `Packages` and `ProjectSettings` contain everything to run the game in the unity editor. The game can be build for android and loaded onto a Quest 3. This is not recommended since there are bugs in the game that will halt progress, but can be fixed from inside the unity editor. For an enjoyable experience use a long usb cable and have players tethered to the pc.
The starting scene is the MainMenu scene, while the levels are in the GameScene scene.

# Misc.
- There's a two minute trailer for the game in 360p under `404_not found_small.mp4`
- The final presentation slides are under `PVRU Final Presentation.pdf`
- Credits can be found under `Assets/Credits/Credits.txt`

# Space-Engineers-Automatic-Dynamic-Braking
A Space Engineers Programmable Block Script for automated safe travelling between two points.

From Space Engineers Official Steam Page (https://store.steampowered.com/app/244850/Space_Engineers/): "Space Engineers is a sandbox game about engineering, construction, exploration and survival in space and on planets. Players build space ships, space stations, planetary outposts of various sizes and uses, pilot ships and travel through space to explore planets and gather resources to survive." Space Engineers features a programmable block which offers opportunities for automation or ease of life task , which the script(s) here utilize to accomplish their task. These scripts are programmed in C#.

In Space Engineers, there can be a lot of travelling from one asteroid or spacestation to the next. A regular annoyance with this task is that since the game is entirely physics based, you usually either A. stop/slow down too early and then have to keep inching your way towards your destination, or B. You attempt to slow down too late, resulting in potentially extremely dangerous crashes.

Utilizing the camera block's raycast functionality, we can input our position in space, the position of where were we want to go, the ship's mass, and the amount of thrust (in force (N)) pointed backwards; and using vector analysis and basic physics, we can calculate exactly when the ship should start slowing down. Saving time without compromising safety. 

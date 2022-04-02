# DogScepter
*(Icon courtesy of Msnshame and Agentalex9! Inspired by UNDERTALE artwork.)*

<img src="icon.png" alt="icon" width="15%" height="15%"> 

DogScepter is a work-in-progress modding tool geared toward (but not limited to) UNDERTALE/DELTARUNE by Toby Fox.

\* Join the Underminers community Discord server! https://discord.gg/RxXpdwJ

## Features
* Fully supports serializing/deserializing GameMaker data format ID 13 (an unknown version of GM:S 1) up to the GMS 2022.3 runtime (format ID 17).
	* Note: There are some exceptions to this, with certain versions being undocumented or using unknown features.
* An "abstract"/"project" mode that deals with a custom project format on disk for ease in modding, even with multiple versions of a game.
* GML VM decompilation, for use in re-compiling back for modding purposes.
* Work-in-progress command line interface to deal with the aforementioned "project" mode, to deal with assets and compilation.
* Work-in-progress editor similar to that of UndertaleModTool, but simplified.

## Roadmap (subject to change)
- [x] Core library
    - Fully parse GameMaker IFF data files, store in memory, and be able to serialize them to new files byte-for-byte.
- [ ] Project system **(currently a work in progress)**
    - Convert from GameMaker structures into custom ones
    - Convert from custom structures back into GameMaker structures
    - Save and load these custom structures to JSON as necessary
    - Assets that are currently supported (as of last README update):
    	- General info/options
    	- Audio groups
    	- Texture groups (and dealing with textures in general)
    	- Paths
    	- Sounds
    	- Objects
    	- Backgrounds
    	- Sprites
    	- Fonts
    	- Rooms
- [ ] Command line interface **(currently a work in progress)**
     - Simple CLI to allow actual work to be done with the above project system before everything else is done, including the UI itself. 
	 - As of writing this, the CLI has basic features and is somewhat usable with (or without) an interactive shell.

## Special Thanks
* Msnshame and Agentalex9 for assistance in creating the icon for the tool. Additional thanks to the Underminers Discord server members for being there to come up with the name, as well.
* Previous GameMaker/Undertale data format research and tools
    - https://pcy.ulyssis.be/undertale/
    - https://github.com/donkeybonks/acolyte/wiki/Bytecode
    - https://github.com/PoroCYon/Altar.NET
    - https://github.com/WarlockD/GMdsam
	- Additional research by Nik (@nkrapivin)
    - Most prominently, the reason this now exists (and also major inspiration for core code), https://github.com/krzys-h/UndertaleModTool

## Contributors
- colinator27 (me)
- TheEternalShine

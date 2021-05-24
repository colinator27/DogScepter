# DogScepter
*(Icon courtesy of Msnshame and Agentalex9! Inspired by UNDERTALE artwork.)*

<img src="icon.png" alt="icon" width="15%" height="15%"> 

\* (This will eventually be a complete README...)

\* (The lack of a proper description fills you with DETERMINATION.)

\* Join the Underminers community Discord server! https://discord.gg/RxXpdwJ

\* (Oh, right, this is just a work-in-progress modding tool geared toward (but not limited to) UNDERTALE/DELTARUNE by Toby Fox.)

## Features
* Right now, reading/writing the barebones data file is in progress. It's really early in development...
* Planned features include an actual editor, a superior editing mode for modding purposes, and eventually decompilation/compilation (potentially).

## Roadmap (subject to change)
- [x] Core library
    - Fully parse GameMaker IFF data files, store in memory, and be able to serialize them to new files byte-for-byte.
	- Fully supports format ID 14 (an unknown version of GM:S 1.4) up to the latest GMS 2.3.2 runtime!
- [ ] Project system (current focus)
    - Convert from GameMaker structures into custom ones
    - Convert from custom structures back into GameMaker structures
    - Save and load these custom structures to JSON as necessary
    - Assets that are currently supported as of writing:
    	- General info/options
    	- Audio groups
    	- Texture groups (and dealing with textures in general)
    	- Paths
    	- Sounds
    	- Objects
    	- Backgrounds
- [ ] Command line interface
     - Simple CLI to allow actual work to be done with the above project system before everything else is done, including the UI itself. 

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

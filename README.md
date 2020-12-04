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
- [ ] Stage 1: Core library (work in progress)
    - Fully parse GameMaker IFF data files, store in memory, and be able to serialize them to new files byte-for-byte.
- [ ] Stage 2: Abstraction layer in library
    - [ ] Convert the output of the Core into a project-esque format, where IDs resolve to asset names, strings become unique, textures get split, and so on.
    - [ ] Be able to convert this *back* into the structures of the Core.
	- Texture page generation
	- Re-compress strings
	- Resolve asset names to IDs
- [ ] Stage 3: System for exporting/importing specific assets automatically for source control

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

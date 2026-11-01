# ItemPack

A Risk of Rain 2 item pack mod built with BepInEx and R2API.

## Building

Run `dotnet build` in `ItemPack/`. Debug builds copy the DLL into the `ModTest` Thunderstore Mod Manager profile. Release builds (`dotnet build -c Release`) also create the Thunderstore upload zip at `ItemPack/bin/Release/ItemPack.zip`.

## Adding an item

1. Copy `ItemPack/Items/OatmilkLatte.cs` to a new file and rename the class, item name and language tokens.
2. Add `new YourItem().Init();` in `ItemPackPlugin.Awake()`.
3. Add it to the item table in `Thunderstore/README.md`.

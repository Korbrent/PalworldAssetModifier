# PalworldAssetModifier
Uses the UAssetAPI to modify datatables in Palworld. 

# Requirements:
- `UAssetAPI` - [atenfyr/UAssetAPI](https://github.com/atenfyr/UAssetAPI)
    - See [https://atenfyr.github.io/UAssetAPI/guide/basic.html](https://atenfyr.github.io/UAssetAPI/guide/basic.html) for further information on how to set it up.
- `FModel` - [fmodel.app](https://fmodel.app/)
    - See [https://pwmodding.wiki/docs/asset-swapping/StartingOut](https://pwmodding.wiki/docs/asset-swapping/StartingOut) for further information on how to set it up.
## Optional:
- `UnrealPak`
    - This seems to be downloadable from many locations, though I downloaded mine from [here](https://github.com/RiotOreO/unrealpak).

# How to use:

To use this program:
- First, set up your config file. (See the "**Setting up the config**" section).
- After the config has been set up to your liking, run `PalworldAssetModifier.exe`.
- The files will be output to your configured `outputDir`.
- If automatic packing was enabled, the `.pak` file will be located in the same directory as `outputDir`. 

## Setting up the config
The config should be named `config.json` and located in the same location as the `.exe` program. An `exampleConfig.json` has been provided. 
- Open the config and modify the following:
    - `exportsDir` : The directory of your FModel exports file.
        - *Ex* : `D:\\PalworldModding\\Tools\\FModel\\Output\\Exports`
        - *Note* : Assuming you followed the instructions to set up FModel, then see [here](https://pwmodding.wiki/docs/datatable-modding/uassetgui/UAssetGuide1#step-1-exporting-the-file-we-want-to-edit) about generating the Exports.
    - `outputDir` : The directory to output to. If this directory doesn't exist, it will be created.
        - *Ex* : `D:\\PalworldModding\\Out\\ExampleMod_P`
    	- *Note* : This should be the top level directory. This should not include the subdirectory asset path. 
    - `mappingPath` : The path to the `.usmap` mapping file.
        - *Ex* : `D:\\PalworldModding\\Mappings.usmap`
        - If you do not have this file, it can be obtained from [here](https://github.com/PalworldModding/UsefulFiles/raw/refs/heads/master/Mappings.usmap).

- If you want it to automatically pack your mod into a `.pak` file...
    - `doUnrealPak` : This should be true if you want automated packing.
    - `unrealPakDir` : This should be the directory that `UnrealPak.exe` is located in.
        - *Ex* : `D:\\PalworldModding\\Tools\\UnrealPak`
    - *Note* : The `.pak` file will be located in the same directory as your `outputDir`.
        - *Ex* : If `outputDir` is `D:\\PalworldModding\\Out\\ExampleMod_P`, then the `.pak` file will be located at `D:\\PalworldModding\\Out\\ExampleMod_P.pak`.

- The following values should be modified if you intend to adjust the mods drop rates and multipliers:
    - `weightMultiplier` : The multiplier value to adjust Lvl 4 blueprints to.
        - Every chest "Grade" in Palworld has several "Slots", each Slot contains multiple items, each item with their own individual weight in that Slot. The probability of an item to drop from a Slot is thus equivalent to: `item.WeightInSlot / slot.TotalWeight`.
        - The default weight values and drop chances can be found [here](https://palworld.fandom.com/wiki/Treasure_Chest).
        - If you wanted to calculate the probability of a specific item dropping from a chest Grade, then you could do the following:
            - Multiply the probability of failure for each slot, we'll call this `q_tot` (recall: `q = 1 - p` from basic statistics).
            - This gives you the total chance to fail across slots, thus the chance to succeed across at least one slot is `1 - q_tot`.
            - Here's a pseudocode equivalent: `p_fail = 1; for Slot in Grade: p_fail *= (1 - Slot.item.p_success); Grade.item.p_success = 1 - p_fail`
            - Thank you for attending my Basic Probabilities lecture.
    - `minWeight` : The minimum weight value to set level 4 blueprints to. 
        - If the weight of the blueprint after the multiplier is less than this value, it will be bumped up to this value.
    - `expeditionDropMultiplier` : The amount to multiply expedition drops by.
    - `cashChunkAmount` : The amount to chunk cash drops into. (This includes DogCoin and Money.)
        - Setting this to `-1` will leave this parameter alone.
        - This doesn't change the min or max amount earned from chests.
    - `cashMultiplier` : The amount to multiply cash drops by.
    - `softCap` : If any drop multipliers result in a value higher than this, it will be set to this.
        - *Note* : Necessary to avoid crashing, as too high of a quantity of drops will result in crashing.

- Optionally, you can modify the following, though it should not be necessary unless you intend to change the functionality of the program:
    - `assetPath` : This should be the subdirectory structure to the asset.
        - *Ex* : `\\Pal\\Content\\Pal\\DataTable\\Item`
        - *Note* : 99% of the time, this will start with `Pal\\Content\\Pal\\DataTable` and you will only be changing the last directory.
    - `assetName` : The name of the `.uasset` file to modify.
        - *Ex* : `DT_ItemLotteryDataTable.uasset`
        - *Note* : As described [here](https://pwmodding.wiki/docs/datatable-modding/uassetgui/UAssetGuide1), if the file has an `_Common` variant, that one should be used.
    - `tableName` : The name of the table to modify. This is typically the same as the assetName without ".uasset" at the end.

## Setting up the project 
If you intend to modify the program, you must follow these steps.
However, if you do not plan on changing the code and only want to adjust the mod's drop rates, you can skip this section.
- Create a new "C# Console App" in Visual Studio with the Framework set to ".NET 8.0"
- In the "Solution Explorer" tab, right-click on "Dependencies", add a project reference, and set the location to the `UAssetAPI.dll`.
- See [the UAssetAPI guide](https://atenfyr.github.io/UAssetAPI/guide/basic.html) for further help on the previous steps.
- Use my `Program.cs` file.
- Enjoy messing with the code. I don't often program in C#, so I am certain I have committed a couple language-specific sins.

# See More:
Refer to the following for more information:
- [Palworld Modding wiki guide on UAsset modding](https://pwmodding.wiki/docs/datatable-modding/uassetgui/UAssetGuide1)
- [UAssetAPI Docs](https://atenfyr.github.io/UAssetAPI/index.html)

Feel free to contact me on Discord if you need further help.
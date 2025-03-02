/***
 * Author:          Korbrent
 * Project:         PalworldAssetModifier
 * Version:         V0.1.0
 * Current Date:    3/1/2025
 * First Date:      2/27/2025
 * 
 * Description:     Modify `.uasset` files using the UAssetAPI.
 *
 * See also:        https://pwmodding.wiki/docs/datatable-modding/uassetgui/UAssetGuide1
 *                  https://atenfyr.github.io/UAssetAPI/index.html
 */
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;
using UAssetAPI;
using UAssetAPI.PropertyTypes.Structs;
using System.Diagnostics;
using Newtonsoft.Json;

namespace PalworldAssetModifier
{
    /**
     * Config class.
     * Loads in the config located at `./config.json` and maintains config information
     */
    class Config
    {
        // UnrealEngine version
        public string engineVersion { get; set; }
        // Amount to multiply cash by
        public double cashMultiplier { get; set; }
        // Amount to multiply expedition drops by
        public double expeditionDropMultiplier { get; set; }
        // Amount to chunk cash into so that it will only drop as a multiple of this number.
        public int cashChunkAmount { get; set; }
        // Cap for which min and maxNum abide by
        public int softCap { get; set; }
        // Amount to multiply weight of lvl 4 schematics by
        public double weightMultiplier { get; set; }
        // Min weight of lvl 4 schematics
        public double minWeight { get; set; }
        // Name table we are modifying
        public string tableName { get; set; }
        // Path to the `.usmap` file.
        public string mappingPath { get; set; }
        // Path to the FModel exports directory
        public string exportsDir { get; set; }
        // The asset path. This typically will be "/Pal/Content/Pal/..."
        public string assetPath { get; set; }
        // Name of asset to modify
        public string assetName { get; set; }
        // Directory to output new files
        public string outputDir { get; set; }
        // Automatically package after completion?
        public bool doUnrealPak { get; set; }
        // Directory to unrealPak (necessary only if doUnrealPak is true)
        public string unrealPakDir { get; set; }

        /**
         * Load the config from the config file
         */
        static public Config LoadConfig()
        {
            string configPath = "config.json";
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"Config file not found! Expected at: {Path.GetFullPath(configPath)}");
                return null;
            }

            string json = File.ReadAllText(configPath);
            Config config = JsonConvert.DeserializeObject<Config>(json);
            if (config is null)
                return config;

            // Trim any ending directory slashes. 
            config.outputDir = config.outputDir.TrimEnd('\\', '/');
            config.exportsDir = config.exportsDir.TrimEnd('\\', '/');
            config.assetPath = config.assetPath.TrimEnd('\\', '/');
            config.unrealPakDir = config.unrealPakDir.TrimEnd('\\', '/');
            return config;
        }
    }

    /**
     * The main program class.
     */
    class Program
    {
        static private Config cfg = Config.LoadConfig();

        /**
         * Packs the outputDir using UnrealPak.exe located at unrealPakDir
         * 
         * Output file will be located at outputDir/..
         * and will be named "{outputDirName}.pak"
         */
        static void unrealPak()
        {
            // Write filelist.txt
            string filelistPath = Path.Join(cfg.unrealPakDir, "filelist.txt");
            string fileContent = $"\"{cfg.outputDir}\\*.*\" \"..\\..\\..\\*.*\"";
            File.WriteAllText(filelistPath, fileContent);
            Console.WriteLine($"Filelist written at location: {filelistPath}");

            // Run unrealPak
            string unrealPakExe = Path.Join(cfg.unrealPakDir, "UnrealPak.exe");
            string pakFile = $"{cfg.outputDir}.pak";
            Console.WriteLine($"Creating pakfile {pakFile}");

            // Create the process start info
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = unrealPakExe,
                Arguments = $"\"{pakFile}\" -create=filelist.txt",
                WorkingDirectory = cfg.unrealPakDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Run a process using the start info
            using (Process process = new Process { StartInfo = psi })
            {
                process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
                process.ErrorDataReceived += (sender, args) => Console.Error.WriteLine(args.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }
        }

        /**
         * Writes out the new asset files to the outputDir
         * If outputDir does not exist, then it will create it.
         */
        static void writeOut(UAsset asset)
        {
            string outputDir = cfg.outputDir,
                assetPath = cfg.assetPath;
            if (outputDir.TrimEnd('\\', '/').EndsWith(assetPath, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"outputDir contained assetPath, removing assetPath from outputDir.\n\tOutputDir:{outputDir}");
                outputDir = outputDir.Substring(0, outputDir.Length - assetPath.Length).TrimEnd('\\', '/');
                Console.WriteLine($"New outputDir: {outputDir}");
                cfg.outputDir = outputDir;
            }

            if (!Directory.Exists(outputDir))
            {
                Console.WriteLine($"outputDir does not exist. Creating it now at {outputDir}");
                Directory.CreateDirectory(outputDir);
            }

            if (!Directory.Exists(Path.Join(outputDir, assetPath)))
            {
                Console.WriteLine($"outputDir/assetPath does not exist. Creating it now at {Path.Join(outputDir, assetPath)}");
                Directory.CreateDirectory(Path.Join(outputDir, assetPath));
            }

            string output = Path.Join(outputDir, assetPath, cfg.assetName);
            asset.Write(output);
            Console.WriteLine("File written to " + output);
        }

        /**
         * Modifies the droprate of a Blueprint item
         * 
         * Modifying droprate is as simple as changing the WeightInSlot of an item.
         * 
         * Each Grade of chest has several Slots in them.
         * Each slot drops an item, where the item's drop rate in that slot is p[item] = (item.WeightInSlot / slot.TotalWeight)
         * Calculating the chance to get an item in a chest is as simple as multiplying (1 - p[item]) for each slot, then subtracting 1 by the resulting value
         */
        static void modifyBP(StructPropertyData e, string itemName, string fieldName)
        {
            double weight_old = -1,
                weight_new = -1,
                weightMultiplier = cfg.weightMultiplier,
                minWeight = cfg.minWeight;
            PropertyData prop;

            Console.Write(String.Format("EntryName: {0:S}\tFieldName: {2:S}\tItemID: {1:S}", e.Name, itemName, fieldName));
            //"\tProperty Type:" + e.PropertyType + "\tStructType: " + e.StructType);

            // WeightInSlot will be a FloatProperty, as we can see in UAssetGUI. 
            prop = e["WeightInSlot"];
            if (prop is FloatPropertyData floatprop)
            {
                weight_old = (double)floatprop.Value;
                floatprop.Value *= (float)weightMultiplier;
                weight_new = (double)floatprop.Value;
            }
            // This next block is useless, however it is here for sanity checking.
            else if (prop is DoublePropertyData doubleprop)
            {
                weight_old = doubleprop.Value;
                doubleprop.Value *= weightMultiplier;
                weight_new = doubleprop.Value;
            }

            if ((weight_new < minWeight) && (minWeight > 0))
            {
                if (prop is FloatPropertyData fp)
                    fp.Value = (float)minWeight;
                if (prop is DoublePropertyData dp)
                    dp.Value = minWeight;
                weight_new = minWeight;
            }

            Console.Write(String.Format("\tWeight: {0:F2} -> {1:F2} ({2:F2}%)\n", weight_old, weight_new, ((weight_new - weight_old) / weight_old) * 100));
        }

        /**
         * Modifies a row by adjusting the minNum and maxNum to increase amount of drops
         * 
         * This does not increase the actual drop rate, only the quantity that drops.
         * Modifying the drop rate is as simple as changing the WeightInSlot parameter.
         */
        static void modifyOther(StructPropertyData e, string itemName, string fieldName, double mult, int chunk)
        {
            PropertyData prop;
            int val;
            int softCap = cfg.softCap;

            prop = e["MinNum"];
            if (!(prop is IntPropertyData))
            {
                Console.WriteLine("MinNum: " + prop.PropertyType);
                Environment.Exit(5);
            }
            val = (int)(((IntPropertyData)prop).Value * mult);
            ((IntPropertyData)prop).Value = (val < softCap ? val : softCap);

            prop = e["MaxNum"];
            if (!(prop is IntPropertyData))
            {
                Console.WriteLine("MaxNum: " + prop.PropertyType);
                Environment.Exit(5);
            }
            val = (int)(((IntPropertyData)prop).Value * mult);
            ((IntPropertyData)prop).Value = (val < softCap ? val : softCap);

            prop = e["NumUnit"];
            if (!(prop is IntPropertyData))
            {
                Console.WriteLine("NumUnit: " + prop.PropertyType);
                Environment.Exit(5);
            }
            if (fieldName.StartsWith("Expedition"))
            {
                // Multiply for expeditions
                val = (int)(((IntPropertyData)prop).Value * mult);
                ((IntPropertyData)prop).Value = (val < softCap ? val : softCap);
            }
            else if (chunk > 0)
            {
                // Set to chunk amount if set (for cash)
                val = chunk;
                ((IntPropertyData)prop).Value = (val < softCap ? val : softCap);
            }
            Console.Write(String.Format("EntryName: {0:S}\tFieldName: {2:S}\tItemID: {1:S}", e.Name, itemName, fieldName));
            Console.WriteLine("\tValues modded");
            return; // Next row
        }

        /**
         * Filters based on fieldNames.
         * See internal comment for why this function exists.
         */
        static bool fieldNameFilter(string fieldName)
        {
            /* The mod crashes if the value of [min,max]Num is too high. 
             * I can't find an exact upper limit for it.
             * Disabling the following fields seems to help a tad.
             *  -   Without filtering, the mod would crash at a softCap of 25000 and sometimes at 15k
             *  -   With filtering, the mod would sometimes crash at a softCap of 25000 but safe at 15k
             *  
             * I dont see a benefit in wasting more time debugging this or finding exactly what the limit is before crashing.
             * I have spent a lot of time with trial-and-error and discovered a soft-cap of ~15k is safe.
             */
            List<String> fieldNameFilters = new List<String>();
            fieldNameFilters.Add("Dev");
            fieldNameFilters.Add("CharacterSpawn");

            bool passedTest = true;
            foreach (String filter in fieldNameFilters)
            {
                if (fieldName.StartsWith(filter))
                {
                    Console.WriteLine("Skipping due to filter");
                    passedTest = false;
                    break;
                }
            }
            return passedTest;
        }

        /**
         * Handle a single row of data from our table
         */
        static void handleRow(StructPropertyData e)
        {
            // Get the property (which is like a cell in the table) that corresponds to this row's StaticItemId
            // We can see in UAssetGUI that this is supposed to be a NameProperty
            PropertyData prop = e["StaticItemId"];
            String itemName = null;
            if (prop is NamePropertyData)
            {
                itemName = ((NamePropertyData)prop).Value.ToString();
            }
            if (itemName is null)
                return; // Next row

            // Get the FieldName property (same as above)
            prop = e["FieldName"];
            String fieldName = "N/A";
            if (prop is NamePropertyData)
            {
                fieldName = ((NamePropertyData)prop).Value.ToString();
            }

            // I mostly just want to increase the droprates of Lvl 4 schematics. 
            if ((itemName.EndsWith("_5") && itemName.StartsWith("Blueprint")))
            {
                modifyBP(e, itemName, fieldName);
                return;
            }

            // I want to increase the amount of Money and DogCoin drops though, 
            // as well as the drops from expeditions. So I will check if the ItemName is currency or if the Field is an Expedition
            if (!((itemName.Equals("Money") || itemName.Equals("DogCoin")) || fieldName.StartsWith("Expedition")))
                return; // Next row

            if (fieldNameFilter(fieldName))
                return;

            // If this row is an Expedition item drop, we want to increase the drops.
            if (fieldName.StartsWith("Expedition"))
            {
                modifyOther(e, itemName, fieldName, cfg.expeditionDropMultiplier, -1);
            }
            else
            {
                modifyOther(e, itemName, fieldName, cfg.cashMultiplier, cfg.cashChunkAmount);
            }
            return;

        }

        static void Main(string[] args)
        {
            if (cfg is null)
            {
                Environment.Exit(1);
            }

            // We can't have a negative multiplier, only valid input is [0, inf)
            if (cfg.cashMultiplier < 0 || cfg.expeditionDropMultiplier < 0 || cfg.weightMultiplier < 0)
            {
                Console.WriteLine("Invalid multiplier value (leq zero)");
                Environment.Exit(1);
            }

            // minWeight or cashChunkAmount leq zero means to ignore. Doesnt work for multipliers (they should use 1.0 instead, since x * 1 = x)
            if (cfg.softCap <= 0)
            {
                Console.WriteLine("Invalid softCap value. Trust me, you want this to be set if you are dealing with large numbers. I've gone ahead and set it to 15000 for you.");
                cfg.softCap = 1500;
            }

            if (!Enum.TryParse(cfg.engineVersion, out EngineVersion engine))
            {
                Console.WriteLine($"Invalid engine version specified in config: {cfg.engineVersion}");
                Environment.Exit(2);
            }

            string tableName = cfg.tableName,
                mappingPath = cfg.mappingPath,
                assetDir = (cfg.exportsDir.Contains(cfg.assetPath) ? cfg.exportsDir : Path.Join(cfg.exportsDir, cfg.assetPath)),
                assetName = cfg.assetName,
                outputDir = cfg.outputDir;

            if (!assetDir.EndsWith(assetName))
                assetDir = Path.Join(assetDir, assetName);

            Console.WriteLine("Starting...");

            // Instantiate the asset with the path, engine version, and mapping. 
            UAsset myAsset = new UAsset(assetDir, engine, new UAssetAPI.Unversioned.Usmap(mappingPath));

            // Since there is only 1 Export in the list, we could just do `DataTableExport itemLottery_DT = (DataTableExport)(myAsset.Exports[0])` but this way is more "fool proof"
            DataTableExport itemLottery_DT = null;
            List<Export> exportsList = myAsset.Exports;
            foreach (Export export in exportsList)
            {
                if (tableName.Equals(export.ObjectName.ToString()))
                {
                    Console.WriteLine("Found the correct table");
                    if (!(export is DataTableExport))
                    {
                        Console.WriteLine($"Unexpected type for the DataTable: {export.GetExportClassType()}");
                        Environment.Exit(3);
                    }
                    itemLottery_DT = (DataTableExport)export;
                }
            }

            if (itemLottery_DT is null)
            {
                Console.WriteLine("Error: DataTable is null.");
                Environment.Exit(4);
            }

            // Get the table data from the datatable
            List<StructPropertyData> entries = itemLottery_DT.Table.Data;

            // Iterate through each row in the table
            foreach (StructPropertyData e in entries)
            {
                handleRow(e);
            }

            writeOut(myAsset);

            if (cfg.doUnrealPak)
            {
                Console.WriteLine("Automatic packaging enabled");
                unrealPak();
            }
        }
    }
}
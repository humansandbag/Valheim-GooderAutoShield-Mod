using BepInEx;
using BepInEx.Configuration;
using System.Linq;
using HarmonyLib;
using System.Reflection;
using System;
using Newtonsoft.Json;
using System.IO;
using UnityEngine;

namespace GooderAutoShield
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    [BepInIncompatibility("org.bepinex.plugins.dualwield")]
    [BepInIncompatibility("vapok.mods.shieldmebruh")]
    [BepInIncompatibility("tech.zinals.valheim.shieldautoequip")]
    internal class GooderAutoShield : BaseUnityPlugin
    {
        public const string PluginGUID = "MainStreetGaming.GooderAutoShield";
        public const string PluginName = "GooderAutoShield";
        public const string PluginVersion = "1.0.3";

        public static ConfigEntry<bool> _instantEquip;
        public static ConfigEntry<bool> _autoUnequip;
        public static ConfigEntry<bool> _enableDebug;
        public static ItemDrop.ItemData currentShield;
        public static ItemDrop.ItemData lastShield;
        public static bool toggleUnequipShield = false;
        public static string lastUsedShieldItemId = "";
        public static Vector2Int loadLastShieldGridPos;
        public static bool initialDataLoaded = false;

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        //public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();


        private void Awake()
        {
            // Array of 10 funny messages
            string[] messages = {
            "The developers worked tirelessly to create this masterpiece… and then {0} came along and broke it.",
            "Behold, {0} has descended from the virtual heavens to grace us with its presence.",
            "The game just got a whole lot cooler, thanks to the magical powers of {0}.",
            "Don't panic, it's just {0}. No need to sacrifice a goat or anything.",
            "With great power comes great responsibility... and {0} has both.",
            "Ready or not, here comes {0} to take your gameplay experience to the next level… or at least, make it more entertaining.",
            "Some mods just want to watch the world burn. Luckily, {0} is not one of them… we think.",
            "Step aside, mere mortals, {0} has arrived to save the day… or completely wreck it. Who knows?",
            "{0} is here to take our gaming experience to the next level. Buckle up, folks!",
            "We regret to inform you that your gameplay experience will never be the same again… all thanks to {0}."
             };

            // Generate a random index for the message array
            System.Random random = new System.Random();
            int messageIndex = random.Next(messages.Length);

            // Use String.Format to insert the plugin name into the message
            string message = String.Format(messages[messageIndex], PluginName);

            // Jotunn comes with its own Logger class to provide a consistent Log style for all mods using it
            Jotunn.Logger.LogInfo(message);

            // To learn more about Jotunn's features, go to
            // https://valheim-modding.github.io/Jotunn/tutorials/overview.html

            CreateConfigValues();
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private void CreateConfigValues()
        {
            //ConfigurationManagerAttributes isAdminOnly = new ConfigurationManagerAttributes { IsAdminOnly = true };

            _enableDebug = Config.Bind("Debug", "DebugMode", false, "Enable debug logging.");
            _instantEquip = Config.Bind("Local Config", "InstantEquip", false, "Skip the equip animation.");
            _autoUnequip = Config.Bind("Local Config", "AutoUnequip", true, "Automatically unequip the shield when a one-handed weapon is unequipped.");
        }

        public static void DebugLog(string data)
        {
            if (_enableDebug.Value) Jotunn.Logger.LogInfo(data);
        }

        // Define a custom class to store the data
        public class LastUsedShieldSaveData
        {
            public string lastShieldItemID;
            public Vector2Int lastShieldGridPos;
        }

        // Save data as JSON
        public static void SaveData()
        {
            LastUsedShieldSaveData LastUsedShieldSaveData = new LastUsedShieldSaveData
            {
                lastShieldItemID = lastUsedShieldItemId,
                lastShieldGridPos = new Vector2Int(lastShield.m_gridPos.x, lastShield.m_gridPos.y) // Assign Vector2Int directly
            };

            string json = JsonConvert.SerializeObject(LastUsedShieldSaveData); // Serialize object to JSON

            // Get the name of the player
            string playerName = Player.m_localPlayer?.GetHoverName();
            string fileName = playerName + "." + PluginName + ".json";

            DebugLog("Saving shield data for player: " + playerName);

            File.WriteAllText(Path.Combine(BepInEx.Paths.ConfigPath, fileName), json); // Write to JSON file
        }

        // Load data from JSON
        public static void LoadData()
        {

            // Get the name of the player
            string playerName = Player.m_localPlayer?.GetHoverName();

            DebugLog("Loading shield data for player: " + playerName);

            string fileName = playerName + "." + PluginName + ".json";
            string filePath = Path.Combine(BepInEx.Paths.ConfigPath, fileName);

            if (File.Exists(filePath))
            {
                DebugLog("Loading shield data for player: " + playerName);

                // Read JSON from file with specified filename
                string json = File.ReadAllText(filePath);

                LastUsedShieldSaveData LastUsedShieldSaveData = JsonConvert.DeserializeObject<LastUsedShieldSaveData>(json); // Deserialize JSON to object

                lastUsedShieldItemId = LastUsedShieldSaveData.lastShieldItemID;
                loadLastShieldGridPos = new Vector2Int(LastUsedShieldSaveData.lastShieldGridPos.x, LastUsedShieldSaveData.lastShieldGridPos.y); // Assign Vector2Int values to Vector2
                DebugLog("Loaded shield item: " + lastUsedShieldItemId);
                DebugLog("Loaded shield pos: y: " + loadLastShieldGridPos.y + " x: " + loadLastShieldGridPos.x);
            } 
            else
            {
                DebugLog("Failed to load shield data for player: " + playerName);
                return;
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        public class Humanoid_EquipItem_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Humanoid __instance, ItemDrop.ItemData item)
            {
                // Check if this instance is the local player
                if (__instance.GetComponent<Player>() != Player.m_localPlayer)
                    return;

                // Check if the equipped item is a shield
                if (item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                {
                    // Get the shield item ID and name
                    lastShield = item;
                    lastUsedShieldItemId = item.m_shared.m_name;

                    // Save shield data
                    SaveData();
                }
            }
        }

        [HarmonyPatch(typeof(Player), "OnDeath")]
        public class Player_OnDeath
        {
            [HarmonyPrefix]
            public static void Prefix(Player __instance)
            {
                if (Player.m_localPlayer != null || __instance != Player.m_localPlayer)
                    return;

                // Save the last shield data
                SaveData();

                // Reset
                currentShield = null;
                lastShield = null;
                lastUsedShieldItemId = "";
                toggleUnequipShield = false;
            }
        }

        [HarmonyPatch(typeof(Character), "OnDestroy")]
        public static class Character_OnDestroy_Patch
        {
            private static void Prefix(Character __instance)
            {
                // Check if the destroyed character is the local player
                if (__instance == Player.m_localPlayer)
                {
                    string playerName = Player.m_localPlayer?.GetHoverName();

                    DebugLog("Character removed/disconnected from game: " + playerName);
                    DebugLog("Unloading shield data.");

                    initialDataLoaded = false;

                    // Reset
                    currentShield = null;
                    lastShield = null;
                    lastUsedShieldItemId = "";
                    toggleUnequipShield = false;
                }
            }
        }

        [HarmonyPatch(typeof(TombStone), nameof(TombStone.OnTakeAllSuccess))]
        public class TombStone_OnTakeAllSuccess_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(TombStone __instance)
            {
                // Check if the player is the local player
                Player player = Player.m_localPlayer;
                if (player == null)
                    return;

                // Check if the player is the owner of the tombstone
                if (__instance.GetOwner() != player.GetPlayerID())
                    return;

                // Load the LastUsedShieldData from JSON
                LoadData();

                DebugLog("Tombstone: Loading shield data...");

                // Restore
                LoadData();
                lastShield = Player.m_localPlayer.m_inventory.GetItemAt(loadLastShieldGridPos.x, loadLastShieldGridPos.y);
                if (lastShield.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                {
                    lastUsedShieldItemId = lastShield.m_shared.m_name;
                    DebugLog("Tombstone: Loaded Shield ID: " + lastShield.m_shared.m_name);
                }
                else
                {
                    lastShield = null;
                }
            }
        }

        [HarmonyPatch(typeof(Player))]
        public class Player_Patch
        {
            [HarmonyPatch("Update")]
            [HarmonyPostfix]
            public static void Postfix(Player __instance)
            {
                // Check if the player is the local player and is not dead
                if (__instance == Player.m_localPlayer && __instance.IsDead() == false)
                {
                    // Initial attempt to load saved data:
                    if (initialDataLoaded == false)
                    {
                        DebugLog("Player In-game: Loading shield data...");
                        LoadData();
                        initialDataLoaded = true;
                        lastShield = __instance.m_inventory.GetItemAt(loadLastShieldGridPos.x, loadLastShieldGridPos.y);
                        if (lastShield != null && lastShield.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                        {
                            lastUsedShieldItemId = lastShield.m_shared.m_name;
                            DebugLog("Player In-game: Loaded Shield ID: " + lastShield.m_shared.m_name);
                        }
                        else
                        {
                            lastShield = null;
                        }
                    }

                    ItemDrop.ItemData rightHandItem = __instance.GetRightItem();
                    ItemDrop.ItemData leftHandItem = __instance.GetLeftItem();

                    if (leftHandItem == null && rightHandItem == null)
                    {
                        // Both hands are empty, do nothing
                        return;
                    }

                    // Check if last used shield is still available in the players inventory
                    if (lastShield != null && lastUsedShieldItemId != "")
                    {
                        ItemDrop.ItemData currentItem = __instance.GetInventory().GetItemAt(lastShield.m_gridPos.x, lastShield.m_gridPos.y);
                        bool itemFound = false;

                        if (currentItem == null || currentItem.m_shared.m_name != lastUsedShieldItemId)
                        {

                            DebugLog(PluginName + ": Last Shield not found... Must have been moved. Searching for it...");

                            // Get all the shields in the player's inventory (including the hotbar)
                            ItemDrop.ItemData[] shields = __instance.m_inventory.GetAllItems()
                            .Where(item => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                            .ToArray();

                            foreach (ItemDrop.ItemData shield in shields)
                            {
                                if (shield.m_shared.m_name == lastUsedShieldItemId)
                                {
                                    lastShield = shield;
                                    lastUsedShieldItemId = lastShield.m_shared.m_name;
                                    itemFound = true;

                                    DebugLog("Found Last Shield ID: " + lastUsedShieldItemId);
                                    break;
                                }
                            }
                            if (itemFound == false)
                            {
                                DebugLog("Could not find last used shield in inventory!");
                                lastShield = null;
                                lastUsedShieldItemId = "";
                            }
                        }
                    }

                    // Check of the player already has a shield equipped and remember it
                    if (leftHandItem != null)
                    {
                        if (leftHandItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                        {
                            currentShield = leftHandItem;
                            lastShield = currentShield;
                            lastUsedShieldItemId = lastShield.m_shared.m_name;

                            //DebugLog("Last Shield ID: " + lastUsedShieldItemId);
                        }
                    }

                    // Check if both hands are full
                    if (leftHandItem != null && rightHandItem != null)
                    {
                        // Are the equipped items a one-handed weapon and a shield?
                        if (rightHandItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon &&
                        leftHandItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                        {
                            toggleUnequipShield = true;
                        }
                    }

                    // Check is a two-handed weapon is in either hand
                    if (leftHandItem != null || rightHandItem != null)
                    {
                        if (leftHandItem != null)
                        {
                            if (leftHandItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft)
                            {
                                DebugLog(PluginName + ": Left Two-handed weapon is equipped.");
                                if (currentShield != null) currentShield = null;
                                if (toggleUnequipShield == true) toggleUnequipShield = false;
                            }
                        } else if (rightHandItem != null)
                        {
                            if (rightHandItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon)
                            {
                                DebugLog(PluginName + ": Right Two-handed weapon is equipped.");
                                if (currentShield != null) currentShield = null;
                                if (toggleUnequipShield == true) toggleUnequipShield = false;
                            }
                        }
                    }

                    // Check if left hand item is equipped and is not a shield
                    if (leftHandItem != null)
                    {
                        if (leftHandItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield)
                        {
                            DebugLog(PluginName + ": Left hand equipped is not a shield.");
                            if (currentShield != null) currentShield = null;
                            if (toggleUnequipShield == true) toggleUnequipShield = false;
                        }
                    }

                    if (rightHandItem != null)
                    {
                        if (rightHandItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.OneHandedWeapon)
                        {
                            DebugLog(PluginName + ": Right hand item equipped is not a one-handed weapon.");
                            if (currentShield != null) currentShield = null;
                            if (toggleUnequipShield == true) toggleUnequipShield = false;
                        }
                    }

                    // Check if the player has a one-handed weapon equipped but no shield
                    if (rightHandItem != null && leftHandItem == null &&
                    rightHandItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon &&
                    __instance.GetInventory().GetEquippedItems().FirstOrDefault(i => i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield) == null)
                    {
                        EquipLastOrBestShield(__instance);
                    }
                    // Auto unequip
                    else if (leftHandItem != null && rightHandItem == null)
                    {
                        if (_autoUnequip.Value == true)
                        {
                            if (leftHandItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                            {
                                if (currentShield != null && toggleUnequipShield == true)
                                {
                                    __instance.UnequipItem(currentShield, _instantEquip.Value);
                                    currentShield = null;
                                    toggleUnequipShield = false;
                                    DebugLog(PluginName + ": Shield has been auto-unequipped.");
                                }
                            }
                        }
                    }
                    //  Check if the player is currently not holding anything in their hands and currentShield is not null
                    // Redundant
                    else if (leftHandItem == null && currentShield != null && rightHandItem == null)
                    {
                        if (currentShield != null) currentShield = null;
                    }
                }
            }

            public static void EquipLastOrBestShield(Player __instance)
            {
                // If the player previously used a shield, try to equip it again
                if (lastShield != null)
                {
                    // Check if the current shield is different from the last used shield
                    if (lastShield != currentShield)
                    {
                        __instance.EquipItem(lastShield, _instantEquip.Value);
                        currentShield = lastShield;
                        toggleUnequipShield = true;

                        DebugLog("Last used shield changed to (Shield ID): " + lastUsedShieldItemId);
                    }
                }
                else
                {
                    // Get all the shields in the player's inventory (including the hotbar)
                    ItemDrop.ItemData[] shields = __instance.m_inventory.GetAllItems()
                        .Where(item => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                        .ToArray();

                    // Find the shield with the highest block power
                    ItemDrop.ItemData bestShield = null;
                    float bestBlockPower = 0f;
                    foreach (ItemDrop.ItemData shield in shields)
                    {
                        float blockPower = shield.m_shared.m_blockPower;
                        if (blockPower > bestBlockPower)
                        {
                            bestShield = shield;
                            bestBlockPower = blockPower;
                        }
                    }

                    // Equip the best shield if one is available
                    if (bestShield != null && bestShield != currentShield)
                    {
                        __instance.EquipItem(bestShield, _instantEquip.Value);
                        currentShield = bestShield;
                        lastShield = currentShield;
                        toggleUnequipShield = true;
                        lastUsedShieldItemId = lastShield.m_shared.m_name;

                        DebugLog("Last used shield changed to(Shield ID): " + lastUsedShieldItemId);
                    }
                }
            }
        }
    }
}


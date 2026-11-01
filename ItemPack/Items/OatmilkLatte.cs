using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ItemPack.Items
{
    public class OatmilkLatte
    {
        // The item's definition. Static so there's a single copy, and so the
        // static stat hook below can reach it.
        public static ItemDef itemDef;

        // Tweak the item's numbers here, then rebuild. The description updates to match.
        // const = baked in at compile time. The 'f' suffix makes it a float.
        public const float regenPerStack = 1.0f;         // HP/s per stack at level 1
        public const float regenPerLevelPerStack = 0.2f; // extra HP/s per stack for each level after 1
        public const float speedPerStack = 0.07f;        // 0.07 = +7% movement speed per stack

        // Called once from the main plugin class (ItemPack.cs) when the mod loads.
        public void Init()
        {
            // ItemDef is a Unity ScriptableObject (a data asset), so it must be
            // created with CreateInstance rather than 'new ItemDef()'.
            itemDef = ScriptableObject.CreateInstance<ItemDef>();

            // Internal ID. Must be unique. Players never see it, but it shows up
            // in logs and console commands like give_item.
            itemDef.name = "OATMILKLATTE";

            // Language tokens: lookup keys, not the actual text. The real English
            // strings are attached to these with LanguageAPI.Add further down.
            itemDef.nameToken = "ITEMPACK_OATMILKLATTE_NAME";         // item title
            itemDef.pickupToken = "ITEMPACK_OATMILKLATTE_PICKUP";     // short line shown when you grab it
            itemDef.descriptionToken = "ITEMPACK_OATMILKLATTE_DESC";  // full text in logbook / on hover
            itemDef.loreToken = "ITEMPACK_OATMILKLATTE_LORE";         // logbook flavor text

            // Tier1 = common (white) item.
            // deprecatedTier is marked obsolete (the newer way needs a loaded ItemTierDef
            // asset), but setting it still works. The pragmas hide the two warnings it
            // causes: CS0618 = "obsolete", Publicizer001 = from the tool that exposes
            // the game's private fields. 'restore' turns them back on right after.
#pragma warning disable CS0618, Publicizer001 // deprecatedTier is still the simplest way to pick a vanilla tier
            itemDef.deprecatedTier = ItemTier.Tier1;
#pragma warning restore CS0618, Publicizer001

            // Tags put the item in categories (e.g. category-specific 3D printers, item filters).
            // Healing + Utility, since it does both regen and movement speed.
            itemDef.tags = new[] { ItemTag.Healing, ItemTag.Utility };

            // true = printers, scrappers and Cleansing Pools can take it.
            itemDef.canRemove = true;

            // false = shows in the inventory bar. Hidden items are for internal tracking.
            itemDef.hidden = false;

            // Placeholder "?" icon and model until we have real art.
            // LoadAssetAsync starts loading in the background; WaitForCompletion()
            // blocks until it's done so the asset is ready on the next line.
            // Blocking is fine here because this runs during mod startup.
            itemDef.pickupIconSprite = Addressables.LoadAssetAsync<Sprite>("RoR2/Base/Common/MiscIcons/texMysteryIcon.png").WaitForCompletion();
            itemDef.pickupModelPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Mystery/PickupMystery.prefab").WaitForCompletion();

            // Registers the item with R2API, which adds it to the game's item catalog.
            // This is the line that makes the item actually exist in-game.
            // Display rules control where the item appears on each survivor's body;
            // null = none, so the latte won't show on your character.
            ItemAPI.Add(new CustomItem(itemDef, new ItemDisplayRuleDict(null)));

            // Attach English text to each token from above.
            LanguageAPI.Add("ITEMPACK_OATMILKLATTE_NAME", "Oatmilk Latte");
            LanguageAPI.Add("ITEMPACK_OATMILKLATTE_PICKUP", "Regenerate health and move a little faster.");
            // $"..." is an interpolated string: code inside {...} gets calculated.
            //   {regenPerStack:0.##}       -> "1"  (up to 2 decimals, extra zeros dropped)
            //   {speedPerStack * 100f:0.##} -> "7"  (0.07 turned into a percentage)
            //   <style=cIsHealing> = RoR2 rich text, green healing color.
            //   <style=cIsUtility> = blue utility color.
            //   <style=cStack>     = gray "per stack" color.
            // The two $"..." pieces are joined with + into one string.
            // Like vanilla items, the text only shows the level 1 regen value.
            LanguageAPI.Add("ITEMPACK_OATMILKLATTE_DESC",
                $"Increase <style=cIsHealing>base health regeneration</style> by <style=cIsHealing>+{regenPerStack:0.##} hp/s</style> <style=cStack>(+{regenPerStack:0.##} hp/s per stack)</style>. " +
                $"Increase <style=cIsUtility>movement speed</style> by <style=cIsUtility>{speedPerStack * 100f:0.##}%</style> <style=cStack>(+{speedPerStack * 100f:0.##}% per stack)</style>.");
            LanguageAPI.Add("ITEMPACK_OATMILKLATTE_LORE", "Extra shot, no sugar. Still warm somehow.");

            // Subscribe to R2API's stat event. Whenever the game recalculates a
            // character's stats (item pickup, buffs, level up, ...), R2API calls
            // OnGetStatCoefficients so we can add our bonus.
            RecalculateStatsAPI.GetStatCoefficients += OnGetStatCoefficients;
        }

        // body = the character whose stats are being calculated (player, ally or enemy).
        // args = where we put our stat changes; R2API sums every mod's contributions
        //        and applies the total.
        // Note: this runs for every CharacterBody, so enemies that get the item
        // (e.g. via Artifact of Evolution) benefit too.
        private static void OnGetStatCoefficients(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            // Some characters have no inventory, so skip them. In Unity, !obj also
            // catches destroyed objects, not just null, so it's the usual way to check.
            if (!body.inventory)
            {
                return;
            }

            // How many Oatmilk Lattes this character holds.
            int count = body.inventory.GetItemCount(itemDef);
            if (count > 0)
            {
                // Flat regen at level 1, plus a bit more each level so it doesn't fall off late game.
                // (R2API 1.2.0 has no levelRegenAdd, so we scale by body.level ourselves.
                //  Stats are recalculated on level-up, so this stays current.)
                // Example: level 10, 1 stack -> 1.0 + 0.2 * 9 = 2.8 HP/s.
                float levelBonus = regenPerLevelPerStack * (body.level - 1f);

                // baseRegenAdd is added to the character's base regen, before any regen
                // multipliers: HEALTH_REGEN ~ (BASE_REGEN + baseRegenAdd) * (REGEN_MULT + regenMultAdd).
                args.baseRegenAdd += (regenPerStack + levelBonus) * count;

                // Additive movement speed multiplier, linear stacking: 3 lattes = +21%.
                // Use += (not =): other items and mods add to these same fields,
                // and = would wipe out their bonuses.
                args.moveSpeedMultAdd += speedPerStack * count;
            }
        }
    }
}

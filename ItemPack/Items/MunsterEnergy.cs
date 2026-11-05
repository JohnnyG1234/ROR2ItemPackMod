using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ItemPack.Items
{
    public class MunsterEnergy
    {
        public static ItemDef itemDef;

        public const float damagePerStack = 0.05f;       // 0.05 = +5% damage per stack
        public const float speedPerStack = 0.10f;        // 0.10 = +10% movement speed per stack

        // Our can texture, embedded inside the DLL (see the EmbeddedResource line in ItemPack.csproj).
        // Embedded resource names are "<default namespace>.<folder>.<file>".
        private const string canTextureResource = "ItemPack.Assets.texMunsterCan.png";
        private const string iconTextureResource = "ItemPack.Assets.mpreviewfinal.png";

        // Called once from the main plugin class (ItemPack.cs) when the mod loads.
        public void Init()
        {
            // ItemDef is a Unity ScriptableObject (a data asset), so it must be
            // created with CreateInstance rather than 'new ItemDef()'.
            itemDef = ScriptableObject.CreateInstance<ItemDef>();

            // Internal ID. Must be unique. Players never see it, but it shows up
            // in logs and console commands like give_item.
            itemDef.name = "MUNSTERENERGY";

            // Language tokens: lookup keys, not the actual text. The real English
            // strings are attached to these with LanguageAPI.Add further down.
            itemDef.nameToken = "ITEMPACK_MUNSTERENERGY_NAME";         // item title
            itemDef.pickupToken = "ITEMPACK_MUNSTERENERGY_DESC";       // shown when you grab it; same stat text as the description
            itemDef.descriptionToken = "ITEMPACK_MUNSTERENERGY_DESC";  // full text in logbook / on hover
            itemDef.loreToken = "ITEMPACK_MUNSTERENERGY_LORE";         // logbook flavor text

            // Tier1 = common (white) item.
            // deprecatedTier is marked obsolete (the newer way needs a loaded ItemTierDef
            // asset), but setting it still works. The pragmas hide the two warnings it
            // causes: CS0618 = "obsolete", Publicizer001 = from the tool that exposes
            // the game's private fields. 'restore' turns them back on right after.
#pragma warning disable CS0618, Publicizer001 // deprecatedTier is still the simplest way to pick a vanilla tier
            itemDef.deprecatedTier = ItemTier.Tier1;
#pragma warning restore CS0618, Publicizer001

            // Tags put the item in categories (e.g. category-specific 3D printers, item filters).
            // Damage + Utility, since it does both damage and movement speed.
            itemDef.tags = new[] { ItemTag.Damage, ItemTag.Utility };

            // true = printers, scrappers and Cleansing Pools can take it.
            itemDef.canRemove = true;

            // false = shows in the inventory bar. Hidden items are for internal tracking.
            itemDef.hidden = false;

            // Our rendered can icon. Falls back to the "?" icon if it can't be loaded.
            // LoadAssetAsync starts loading in the background; WaitForCompletion()
            // blocks until it's done so the asset is ready on the next line.
            // Blocking is fine here because this runs during mod startup.
            itemDef.pickupIconSprite = CanAssets.CreateIconSprite(iconTextureResource)
                ?? Addressables.LoadAssetAsync<Sprite>("RoR2/Base/Common/MiscIcons/texMysteryIcon.png").WaitForCompletion();

            // The 3D model: Energy Drink's can with our texture. Falls back to the "?"
            // model if something goes wrong, so the item still works.
            itemDef.pickupModelPrefab = CanAssets.CreateCanModel("PickupMunsterEnergy", canTextureResource)
                ?? Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Mystery/PickupMystery.prefab").WaitForCompletion();

            // Registers the item with R2API, which adds it to the game's item catalog.
            // This is the line that makes the item actually exist in-game.
            // Display rules control where the item appears on each survivor's body;
            // null = none, so the can won't show on your character.
            ItemAPI.Add(new CustomItem(itemDef, new ItemDisplayRuleDict(null)));

            // Attach English text to each token from above.
            LanguageAPI.Add("ITEMPACK_MUNSTERENERGY_NAME", "Munster Energy");
            // $"..." is an interpolated string: code inside {...} gets calculated.
            //   {damagePerStack * 100f:0.##} -> "5"  (0.05 turned into a percentage,
            //                                         up to 2 decimals, extra zeros dropped)
            //   <style=cIsDamage> = RoR2 rich text, damage color.
            //   <style=cIsUtility> = blue utility color.
            //   <style=cStack>    = gray "per stack" color.
            // The two $"..." pieces are joined with + into one string.
            LanguageAPI.Add("ITEMPACK_MUNSTERENERGY_DESC",
                $"Increase <style=cIsDamage>damage</style> by <style=cIsDamage>{damagePerStack * 100f:0.##}%</style> <style=cStack>(+{damagePerStack * 100f:0.##}% per stack)</style>. " +
                $"Increase <style=cIsUtility>movement speed</style> by <style=cIsUtility>{speedPerStack * 100f:0.##}%</style> <style=cStack>(+{speedPerStack * 100f:0.##}% per stack)</style>.");
            LanguageAPI.Add("ITEMPACK_MUNSTERENERGY_LORE", "Contains 160mg of caffeine and an unspecified amount of cheese. Do not ask about the cheese.");

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

            // How many cans this character holds.
            int count = body.inventory.GetItemCount(itemDef);
            if (count > 0)
            {
                // Additive damage multiplier, linear stacking: 3 cans = +15%.
                args.damageMultAdd += damagePerStack * count;

                // Additive movement speed multiplier, linear stacking: 3 cans = +30%.
                args.moveSpeedMultAdd += speedPerStack * count;
            }
        }
    }
}

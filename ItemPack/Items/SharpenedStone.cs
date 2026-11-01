using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ItemPack.Items
{
    // Common item: +10% base damage per stack
    public class SharpenedStone
    {
        public static ItemDef itemDef;

        // Tweak the item's numbers here, then rebuild. The description updates to match.
        public const float damagePerStack = 0.10f;  // 0.10 = +10% damage per stack

        public void Init()
        {
            itemDef = ScriptableObject.CreateInstance<ItemDef>();
            itemDef.name = "SHARPENEDSTONE";
            itemDef.nameToken = "ITEMPACK_SHARPENEDSTONE_NAME";
            itemDef.pickupToken = "ITEMPACK_SHARPENEDSTONE_PICKUP";
            itemDef.descriptionToken = "ITEMPACK_SHARPENEDSTONE_DESC";
            itemDef.loreToken = "ITEMPACK_SHARPENEDSTONE_LORE";
#pragma warning disable CS0618, Publicizer001 // deprecatedTier is still the simplest way to pick a vanilla tier
            itemDef.deprecatedTier = ItemTier.Tier1;
#pragma warning restore CS0618, Publicizer001
            itemDef.tags = new[] { ItemTag.Damage };
            itemDef.canRemove = true;
            itemDef.hidden = false;

            // Placeholder "?" icon and model until we have real art
            itemDef.pickupIconSprite = Addressables.LoadAssetAsync<Sprite>("RoR2/Base/Common/MiscIcons/texMysteryIcon.png").WaitForCompletion();
            itemDef.pickupModelPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Mystery/PickupMystery.prefab").WaitForCompletion();

            // No display rules, so the item won't show on the survivor's body
            ItemAPI.Add(new CustomItem(itemDef, new ItemDisplayRuleDict(null)));

            LanguageAPI.Add("ITEMPACK_SHARPENEDSTONE_NAME", "Sharpened Stone");
            LanguageAPI.Add("ITEMPACK_SHARPENEDSTONE_PICKUP", "Deal more damage.");
            LanguageAPI.Add("ITEMPACK_SHARPENEDSTONE_DESC", $"Increase base damage by <style=cIsDamage>{damagePerStack * 100f:0.##}%</style> <style=cStack>(+{damagePerStack * 100f:0.##}% per stack)</style>.");
            LanguageAPI.Add("ITEMPACK_SHARPENEDSTONE_LORE", "A whetstone worn smooth by years of use.");

            RecalculateStatsAPI.GetStatCoefficients += OnGetStatCoefficients;
        }

        private static void OnGetStatCoefficients(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body.inventory)
            {
                return;
            }

            int count = body.inventory.GetItemCount(itemDef);
            if (count > 0)
            {
                args.damageMultAdd += damagePerStack * count;
            }
        }
    }
}

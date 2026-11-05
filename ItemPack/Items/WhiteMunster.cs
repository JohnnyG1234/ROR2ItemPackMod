using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ItemPack.Items
{
    public class WhiteMunster
    {
        public static ItemDef itemDef;

        public const float bonusPerStack = 0.20f;    // 0.20 = +20% damage and speed per stack with shield
        public const float penaltyPerStack = 0.20f;  // 0.20 = -20% damage and speed per stack without shield

        private const string canTextureResource = "ItemPack.Assets.WhitemonsterTexReal_512.png";
        private const string iconTextureResource = "ItemPack.Assets.whiteMonsterFinalPreview.png";

        // Called once from the main plugin class (ItemPack.cs) when the mod loads.
        public void Init()
        {
            itemDef = ScriptableObject.CreateInstance<ItemDef>();
            itemDef.name = "WHITEMUNSTER";
            itemDef.nameToken = "ITEMPACK_WHITEMUNSTER_NAME";
            itemDef.pickupToken = "ITEMPACK_WHITEMUNSTER_DESC";        // shown when you grab it; same stat text as the description
            itemDef.descriptionToken = "ITEMPACK_WHITEMUNSTER_DESC";
            itemDef.loreToken = "ITEMPACK_WHITEMUNSTER_LORE";

#pragma warning disable CS0618, Publicizer001 // deprecatedTier is still the simplest way to pick a vanilla tier
            itemDef.deprecatedTier = ItemTier.Lunar;
#pragma warning restore CS0618, Publicizer001

            itemDef.tags = new[] { ItemTag.Damage, ItemTag.Utility };
            itemDef.canRemove = true;
            itemDef.hidden = false;

            itemDef.pickupIconSprite = CanAssets.CreateIconSprite(iconTextureResource)
                ?? Addressables.LoadAssetAsync<Sprite>("RoR2/Base/Common/MiscIcons/texMysteryIcon.png").WaitForCompletion();
            itemDef.pickupModelPrefab = CanAssets.CreateCanModel("PickupWhiteMunster", canTextureResource)
                ?? Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Mystery/PickupMystery.prefab").WaitForCompletion();

            ItemAPI.Add(new CustomItem(itemDef, new ItemDisplayRuleDict(null)));

            LanguageAPI.Add("ITEMPACK_WHITEMUNSTER_NAME", "White Munster");
            LanguageAPI.Add("ITEMPACK_WHITEMUNSTER_DESC",
                $"While you have <style=cIsHealing>shield</style>, increase <style=cIsDamage>damage</style> and <style=cIsUtility>movement speed</style> by <style=cIsDamage>{bonusPerStack * 100f:0.##}%</style> <style=cStack>(+{bonusPerStack * 100f:0.##}% per stack)</style>. " +
                $"Without <style=cIsHealing>shield</style>, reduce them by <style=cIsHealth>{penaltyPerStack * 100f:0.##}%</style> <style=cStack>(-{penaltyPerStack * 100f:0.##}% per stack, compounding)</style>.");
            LanguageAPI.Add("ITEMPACK_WHITEMUNSTER_LORE", "Zero sugar. Zero cheese. The can is cold to the touch, and it stays that way.");

            RecalculateStatsAPI.GetStatCoefficients += OnGetStatCoefficients;
            CharacterBody.onBodyInventoryChangedGlobal += OnBodyInventoryChanged;
        }

        private static bool HasShield(CharacterBody body)
        {
            return body.healthComponent && body.healthComponent.shield > 0f;
        }

        private static void OnGetStatCoefficients(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body.inventory)
            {
                return;
            }

            int count = body.inventory.GetItemCount(itemDef);
            if (count <= 0)
            {
                return;
            }

            // With shield, stacking is linear: 3 cans = +60%.
            // Without shield, each can keeps 80% of what's left: 1 can = -20%, 2 = -36%,
            // 3 = -48.8%. A linear penalty would reach -100% at 5 cans, and past that
            // the character would deal negative damage.
            float change = HasShield(body)
                ? bonusPerStack * count
                : Mathf.Pow(1f - penaltyPerStack, count) - 1f;

            args.damageMultAdd += change;
            args.moveSpeedMultAdd += change;
        }

        // Keeps a ShieldWatcher on bodies holding the item. A stack of 0 removes it.
        private static void OnBodyInventoryChanged(CharacterBody body)
        {
            body.AddItemBehavior<ShieldWatcher>(body.inventory.GetItemCount(itemDef));
        }

        // The game only recalculates stats when something marks them dirty, and shield
        // going up or down doesn't. Without this, breaking your shield would leave the
        // bonus active until the next unrelated recalculation.
        private class ShieldWatcher : CharacterBody.ItemBehavior
        {
            private bool hadShield;

            private void FixedUpdate()
            {
                bool hasShield = HasShield(body);
                if (hasShield != hadShield)
                {
                    hadShield = hasShield;
                    body.MarkAllStatsDirty();
                }
            }
        }
    }
}

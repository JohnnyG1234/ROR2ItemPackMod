using System.IO;
using System.Reflection;
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

        // Vanilla Energy Drink's pickup model. We copy it and repaint the copy.
        private const string energyDrinkPickupPath = "RoR2/Base/SprintBonus/PickupSoda.prefab";

        // Our can texture, embedded inside the DLL (see the EmbeddedResource line in ItemPack.csproj).
        // Embedded resource names are "<default namespace>.<folder>.<file>".
        private const string canTextureResource = "ItemPack.Assets.texMunsterCan.png";

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
            itemDef.pickupToken = "ITEMPACK_MUNSTERENERGY_PICKUP";     // short line shown when you grab it
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

            // Placeholder "?" icon until we have a real one.
            // LoadAssetAsync starts loading in the background; WaitForCompletion()
            // blocks until it's done so the asset is ready on the next line.
            // Blocking is fine here because this runs during mod startup.
            itemDef.pickupIconSprite = Addressables.LoadAssetAsync<Sprite>("RoR2/Base/Common/MiscIcons/texMysteryIcon.png").WaitForCompletion();

            // The 3D model: Energy Drink's can with our texture. Falls back to the "?"
            // model if something goes wrong, so the item still works.
            itemDef.pickupModelPrefab = CreateCanModel()
                ?? Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Mystery/PickupMystery.prefab").WaitForCompletion();

            // Registers the item with R2API, which adds it to the game's item catalog.
            // This is the line that makes the item actually exist in-game.
            // Display rules control where the item appears on each survivor's body;
            // null = none, so the can won't show on your character.
            ItemAPI.Add(new CustomItem(itemDef, new ItemDisplayRuleDict(null)));

            // Attach English text to each token from above.
            LanguageAPI.Add("ITEMPACK_MUNSTERENERGY_NAME", "Munster Energy");
            LanguageAPI.Add("ITEMPACK_MUNSTERENERGY_PICKUP", "Unleash the cheese. Deal a little more damage and move faster.");
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

        // Copies Energy Drink's pickup model and swaps in our texture.
        // Returns null (and logs why) if any step fails.
        private static GameObject CreateCanModel()
        {
            GameObject original = Addressables.LoadAssetAsync<GameObject>(energyDrinkPickupPath).WaitForCompletion();
            if (!original)
            {
                Log.Error($"Couldn't load Energy Drink model at '{energyDrinkPickupPath}', using placeholder.");
                return null;
            }

            Texture2D canTexture = LoadEmbeddedTexture(canTextureResource);
            if (!canTexture)
            {
                return null;
            }

            // A prefab is a template the game copies whenever it spawns the object.
            // To make our own template we copy the original, but the copy would show up
            // in the scene and run its scripts. Parenting it under an inactive, never-destroyed
            // holder keeps it hidden and asleep. When the game spawns copies of *our*
            // template, those copies are active, because the holder isn't copied along.
            GameObject clone = Object.Instantiate(original, GetPrefabHolder().transform);
            clone.name = "PickupMunsterEnergy";

            // Give every renderer on the can a new material using our texture.
            // We make *new* materials instead of editing the existing ones, because
            // those are shared with vanilla Energy Drink and would repaint it too.
            foreach (Renderer renderer in clone.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i])
                    {
                        continue;
                    }
                    materials[i] = new Material(materials[i]) { mainTexture = canTexture };
                }
                renderer.sharedMaterials = materials;
            }

            return clone;
        }

        // Reads a PNG embedded in the DLL and turns it into a Unity texture.
        private static Texture2D LoadEmbeddedTexture(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Log.Error($"Embedded texture '{resourceName}' not found. Is it marked as EmbeddedResource in the csproj?");
                    return null;
                }

                byte[] bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    read += stream.Read(bytes, read, bytes.Length - read);
                }

                // The 2x2 size is a placeholder; LoadImage resizes it to the PNG's real size.
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(bytes);

                // Point = no smoothing between pixels, so the pixel art stays crisp.
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.name = System.IO.Path.GetFileNameWithoutExtension(resourceName);
                return texture;
            }
        }

        // An inactive object that survives scene changes, used to store our prefab copies.
        private static GameObject prefabHolder;

        private static GameObject GetPrefabHolder()
        {
            if (!prefabHolder)
            {
                prefabHolder = new GameObject("ItemPackPrefabs");
                prefabHolder.SetActive(false);
                Object.DontDestroyOnLoad(prefabHolder);
            }
            return prefabHolder;
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

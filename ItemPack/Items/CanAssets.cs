using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ItemPack.Items
{
    // Icon and model loading shared by every can item.
    public static class CanAssets
    {
        // Vanilla Energy Drink's pickup model. We copy it and repaint the copy.
        private const string energyDrinkPickupPath = "RoR2/Base/SprintBonus/PickupSoda.prefab";

        // Copies Energy Drink's pickup model and swaps in our texture.
        // Returns null (and logs why) if any step fails.
        public static GameObject CreateCanModel(string modelName, string textureResource)
        {
            GameObject original = Addressables.LoadAssetAsync<GameObject>(energyDrinkPickupPath).WaitForCompletion();
            if (!original)
            {
                Log.Error($"Couldn't load Energy Drink model at '{energyDrinkPickupPath}', using placeholder.");
                return null;
            }

            Texture2D canTexture = LoadEmbeddedTexture(textureResource);
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
            clone.name = modelName;

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

        // Turns an embedded icon PNG into a Sprite, the image type the UI uses.
        // Returns null (and logs why) if the texture can't be loaded.
        public static Sprite CreateIconSprite(string textureResource)
        {
            Texture2D iconTexture = LoadEmbeddedTexture(textureResource);
            if (!iconTexture)
            {
                return null;
            }

            // Bilinear, not Point: the UI draws the icon smaller than the PNG,
            // and Point would make it look jagged.
            iconTexture.filterMode = FilterMode.Bilinear;

            // The Rect uses the whole image; (0.5, 0.5) puts the pivot in the center.
            return Sprite.Create(iconTexture, new Rect(0f, 0f, iconTexture.width, iconTexture.height), new Vector2(0.5f, 0.5f));
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
                texture.name = Path.GetFileNameWithoutExtension(resourceName);
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
    }
}

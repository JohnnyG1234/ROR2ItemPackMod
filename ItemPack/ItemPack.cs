using BepInEx;
using ItemPack.Items;
using R2API;

namespace ItemPack
{
    [BepInDependency(ItemAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency(RecalculateStatsAPI.PluginGUID)]

    [BepInPlugin("com.JohnGroton.ItemPack", "ItemPack", "1.0.0")]

    public class ItemPackPlugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Log.Init(Logger);

            // Register each item here
            new MunsterEnergy().Init();

            Log.Info("ItemPack loaded");
        }
    }
}

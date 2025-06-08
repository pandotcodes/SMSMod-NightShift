using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NightShift
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        private void Awake()
        {
            Instance = this;
            //Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded! Applying patch...");
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
        }
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Semicolon) && Input.GetKey(KeyCode.LeftShift)) InstantRestock();
        }
        public void InstantRestock()
        {
            //Logger.LogWarning("Running instant restock");

            var indexedRackSlots = Singleton<RackManager>.Instance.m_RackSlots.ToDictionary(x => x.Key, x => x.Value);
            foreach (var key in indexedRackSlots.Keys.ToArray())
            {
                indexedRackSlots[key] = indexedRackSlots[key].Where(x => x.m_Rack.gameObject.transform.position.y != 10).ToList();
                //Logger.LogWarning("Indexing product " + key);
            }

            bool anyChangesMade = true;
            while (anyChangesMade)
            {
                anyChangesMade = false;
                // Finding restockable display slots
                List<int> productsInInventory = Singleton<InventoryManager>.Instance.Products.Keys.ToList();
                foreach (var id in productsInInventory)
                {
                    if (id == 0) continue;
                    //if (id != 69) continue;
                    //Logger.LogWarning("Checking product " + id);
                    var productSO = Singleton<IDManager>.Instance.ProductSO(id);
                    List<DisplaySlot> displaySlots = Singleton<DisplayManager>.Instance.GetDisplaySlots(id, false).ToList();
                    //Logger.LogWarning("Found " + displaySlots.Count + " slots");
                    foreach (var displaySlot in displaySlots)
                    {
                        //Logger.LogError("Has " + displaySlot.m_Products.Count + ", needs " + productSO.GridLayoutInStorage.productCount);
                        while (displaySlot.m_Products.Count < ((float)productSO.GridLayoutInStorage.productCount))
                        {
                            int productNeeded = productSO.GridLayoutInStorage.productCount - displaySlot.m_Products.Count;
                            //Logger.LogWarning("Product needs to be restocked: " + productNeeded);

                            var productExists = indexedRackSlots.TryGetValue(id, out var slots);
                            if (!productExists) break;
                            //Logger.LogWarning("Found rack slots");
                            if (slots.Count == 0) break;
                            //Logger.LogWarning("Found rack slot");
                            var slot = slots[Random.RandomRangeInt(0, slots.Count)];
                            if (slot.m_Boxes.Count == 0) break;
                            //Logger.LogWarning("Found boxes");
                            if (!slot.HasProduct) break;
                            //Logger.LogWarning("Found product");
                            if (slot.m_Boxes.Sum(x => x.m_Data.ProductCount) == 0) break;
                            //Logger.LogWarning("Found product in boxes");
                            var box = slot.m_Boxes.Last();

                            anyChangesMade = true;

                            if (box.m_Data.ProductCount <= productNeeded)
                            {
                                displaySlot.Data.FirstItemCount += box.m_Data.ProductCount;
                                displaySlot.SpawnProduct(id, box.m_Data.ProductCount);
                                //Logger.LogWarning("restocked " + box.m_Data.ProductCount + ", deleting box");
                                slot.TakeBoxFromRack();
                                Singleton<InventoryManager>.Instance.RemoveBox(box.Data);
                                LeanPool.Despawn(box.gameObject, 0f);
                                box.ResetBox();
                                Object.Destroy(box.gameObject);
                            }
                            else
                            {
                                displaySlot.Data.FirstItemCount += productNeeded;
                                displaySlot.SpawnProduct(id, productNeeded);
                                //Logger.LogWarning("restocked " + productNeeded + ", keeping box");
                                box.DespawnProducts();
                                box.m_Data.ProductCount -= productNeeded;
                            }
                            slot.SetLabel();
                        }
                        int count = displaySlot.m_Products.Count;
                        foreach (Product clone in displaySlot.m_Products)
                        {
                            
                            if (clone == null) continue;
                            LeanPool.Despawn(clone, 0f);
                        }
                        displaySlot.m_Products.Clear();
                        for (int i = 0; i < count; i++)
                        {
                            Product product = LeanPool.Spawn<Product>(Singleton<IDManager>.Instance.ProductSO(id).ProductPrefab, displaySlot.transform, false);
                            product.transform.localPosition = ItemPosition.GetPosition(Singleton<IDManager>.Instance.ProductSO(id).GridLayoutInStorage, i);
                            product.transform.localRotation = Quaternion.Euler(Singleton<IDManager>.Instance.ProductSO(id).GridLayoutInStorage.productAngles);
                            product.transform.localScale = Vector3.one * Singleton<IDManager>.Instance.ProductSO(id).GridLayoutInStorage.scaleMultiplier;
                            displaySlot.m_Products.Add(product);
                            displaySlot.m_Highlightable.AddOrRemoveRenderer(product.GetComponentsInChildren<Renderer>(true), true);
                        }

                        //int count = displaySlot.Data.FirstItemCount;
                        //displaySlot.Clear();
                        //displaySlot.SpawnProduct(id, count);
                        //displaySlot.Data.FirstItemCount = count;
                        //if(displaySlot.m_Products.Count > productSO.GridLayoutInStorage.productCount)
                        //{
                        //    displaySlot.Data.FirstItemCount = productSO.GridLayoutInStorage.productCount;
                        //}
                        displaySlot.SetLabel();
                        displaySlot.SetPriceTag();
                    }
                }
            }
        }
    }
    public static class NightShiftPatch
    {
        [HarmonyPatch(typeof(DayCycleManager), "FinishTheDay")]
        public static class DayCycleManager_FinishTheDay_Patch
        {
            public static void Prefix()
            {
                Plugin.Instance.InstantRestock();
            }
        }
        [HarmonyPatch(typeof(DisplayManager), "AddDisplaySlot")]
        public static class DisplayManager_AddDisplaySlot_Patch
        {
            public static bool Prefix(DisplayManager __instance, int productID, DisplaySlot newSlot)
            {
                if(__instance.m_DisplayedProducts.ContainsKey(productID))
                {
                    if (__instance.m_DisplayedProducts[productID].Contains(newSlot))
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}

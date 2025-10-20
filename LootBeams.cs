using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
//using static LootBeams.LootBeamEnums;

namespace LootBeams
{
    public class LootBeams : Mod
    {
        public static Dictionary<int, string> modRarities = new Dictionary<int, string>();
        public override void PostSetupContent()
        {
            foreach (ModRarity rarity in ModContent.GetContent<ModRarity>())
                modRarities.Add(rarity.Type, rarity.FullName);
        }
        public override void Unload()
        {
            modRarities.Clear();
        }
    }

    /*public static class LootBeamEnums
    {
        public enum BeamStyle
        {
            None,
            Simple,
            Arrow,
            ArrowAnim
        }
        public enum GlowStyle
        {
            None,
            Simple
        }
    }*/

    //Simplifies initialization as struct
    public struct LootBeamData
    {
        public Vector3 rarityColor;
        public Color beamColor;
        public float fadeIn = 0f;
        public int timeSinceSpawn = 0;
        public float beamAlpha = 0f;

        public int type = ItemID.None;
        public bool init = false;
        public bool beingDrawn = false;

        public LootBeamData(Vector3 rarityColor, Color beamColor, float fadeIn, int timeSinceSpawn, float beamAlpha, int type, bool init, bool beingDrawn)
        {
            this.rarityColor = rarityColor;
            this.beamColor = beamColor;
            this.fadeIn = fadeIn;
            this.timeSinceSpawn = timeSinceSpawn;
            this.beamAlpha = beamAlpha;
            this.type = type;
            this.init = init;
            this.beingDrawn = beingDrawn;
        }
    }

    public class LootBeamPlayer : ModPlayer
    {
        public override void OnEnterWorld()
        {
            //Otherwise when world hopping there's a chance that the beams stay
            LootBeamSystem.Init();
        }
    }

    public class LootBeamSystem : ModSystem
    {
        //Since the mod is only doing visuals, to avoid having to sync instanced GlobalItem data (needed to avoid bugs like resetting the beam), we store our data in a static array
        //to simulate instanced data. We have to manually manage the (de)-initialization of this data
        public static LootBeamData[] lootBeamDataByIndex; //Only works for in-world items

        public static void Init()
        {
            lootBeamDataByIndex = new LootBeamData[Main.maxItems];
        }

        public override void Load()
        {
            Init();
        }

        public override void Unload()
        {
            lootBeamDataByIndex = null;
        }

        public override void PostUpdateItems()
        {
            for (int i = 0; i < lootBeamDataByIndex.Length; i++)
            {
                if (i >= Main.maxItems)
                    return;

                Item item = Main.item[i];
                ref LootBeamData lootBeamData = ref lootBeamDataByIndex[i];

                if (!item.active && lootBeamData.type > ItemID.None)
                {
                    //Reset data of items that despawned
                    lootBeamData = new LootBeamData();
                    continue;
                }

                if (!item.active || item.type == ItemID.None)
                    continue;

                if (lootBeamData.type != item.type)
                {
                    //Refresh/initialize the loot beam of this item
                    lootBeamData = new LootBeamData
                    {
                        type = item.type,
                        init = true
                    };
                }
            }
        }
    }

    public class LootBeamItem : GlobalItem
    {
        static LootBeamConfig config => ModContent.GetInstance<LootBeamConfig>();

        public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            //Edge case: Somehow whoAmI is bigger than the initial length
            if (whoAmI >= LootBeamSystem.lootBeamDataByIndex.Length)
                Array.Resize(ref LootBeamSystem.lootBeamDataByIndex, whoAmI + 1); //No need to init new entries as the type is not a class

            //All ref as we are modifying persistent data
            ref LootBeamData lootBeamData = ref LootBeamSystem.lootBeamDataByIndex[whoAmI];

            if (!lootBeamData.init)
                return base.PreDrawInWorld(item, spriteBatch, lightColor, alphaColor, ref rotation, ref scale, whoAmI);

            ref Vector3 rarityColor = ref lootBeamData.rarityColor;
            ref Color beamColor = ref lootBeamData.beamColor;
            ref float fadeIn = ref lootBeamData.fadeIn;
            ref int timeSinceSpawn = ref lootBeamData.timeSinceSpawn;
            ref float beamAlpha = ref lootBeamData.beamAlpha;

            ref bool beingDrawn = ref lootBeamData.beingDrawn;

            ItemDefinition itemd = new ItemDefinition(item.type);
            if (!config.CustomBlacklist.Contains(itemd))
            {
                #region Color Handling
                // Vanilla colors are hardcoded, there's no accessible way to reference these colors so we simply use our own hardcoded values
                if (item.expert || item.rare == ItemRarityID.Expert)
                    rarityColor = Main.DiscoColor.ToVector3() * new Vector3(255);
                else if (item.master || item.rare == ItemRarityID.Master)
                    rarityColor = new Vector3(255, Main.masterColor * 200, 0f);
                else if (!(item.expert || item.rare == ItemRarityID.Expert) && !(item.master || item.rare == ItemRarityID.Master))
                {
                    rarityColor = item.rare switch
                    {
                        ItemRarityID.Gray => new Vector3(130, 130, 130),
                        ItemRarityID.Blue => new Vector3(150, 150, 255),
                        ItemRarityID.Green => new Vector3(150, 255, 150),
                        ItemRarityID.Orange => new Vector3(255, 200, 150),
                        ItemRarityID.LightRed => new Vector3(255, 150, 150),
                        ItemRarityID.Pink => new Vector3(255, 150, 255),
                        ItemRarityID.LightPurple => new Vector3(210, 160, 255),
                        ItemRarityID.Lime => new Vector3(150, 255, 10),
                        ItemRarityID.Yellow => new Vector3(255, 255, 10),
                        ItemRarityID.Cyan => new Vector3(5, 200, 255),
                        ItemRarityID.Red => new Vector3(255, 40, 100),
                        ItemRarityID.Purple => new Vector3(180, 40, 255),
                        ItemRarityID.Quest => new Vector3(255, 175, 0),
                        _ => new Vector3(255, 255, 255),
                    };
                }
                if (LootBeams.modRarities.TryGetValue(item.rare, out string name))
                    rarityColor = ModContent.Find<ModRarity>(name).RarityColor.ToVector3() * new Vector3(255);
                try
                {
                    if (config.ColorOverrides.ContainsKey(itemd) && config.ColorOverrides.TryGetValue(itemd, out Color color))
                        rarityColor = color.ToVector3() * new Vector3(255);
                }
                catch
                {
                    Mod.Logger.Error("[LootBeams] ItemDefinition or Color is invalid! How did this happen?");
                };
                // Converting Vector3 to Color is weird, hence the necessitated use of division here
                // In my experience, just using the values without division always produces a white color
                beamColor = new Color(rarityColor.X / 255f, rarityColor.Y / 255f, rarityColor.Z / 255f);
                #endregion
                #region Beam Drawing
                Vector2 screenCenter = new Vector2(
                    item.Center.X - Main.screenPosition.X,
                    item.Hitbox.Bottom - Main.screenPosition.Y
                    ); // Remember to subtract by the item's texture frameheight divided by 2 to get the texture's actual center

                if (config.CustomWhitelist.Contains(itemd) || 
                    (config.HighlightQuest && (item.questItem || item.rare == ItemRarityID.Quest)) || 
                    (config.HighlightExpert && (item.expert || item.rare == ItemRarityID.Expert)) || 
                    (config.HighlightMaster && (item.master || item.rare == ItemRarityID.Master)) ||
                    (config.MinRarity > -2 && item.rare >= config.MinRarity && item.value >= config.MinValue && 
                    !(item.questItem || item.rare == ItemRarityID.Quest) && 
                    !(item.expert || item.rare == ItemRarityID.Expert) && 
                    !(item.master || item.rare == ItemRarityID.Master))
                    )
                {
                    Texture2D itemTex = TextureAssets.Item[item.type].Value;
                    int itemFrameHeight = itemTex.Height;
                    if (Main.itemAnimationsRegistered.Contains(item.type))
                    {
                        if (Main.itemAnimations[item.type].FrameCount > 1)
                            itemFrameHeight /= Main.itemAnimations[item.type].FrameCount;
                    }
                    Vector2 itemTexSize = new Vector2(itemTex.Width, itemFrameHeight);

                    float exScale = config.BeamScale;
                    float exGlowScale = config.GlowScale;
                    if (config.UseMiniBeam.Contains(itemd))
                    {
                        exScale = .5f;
                        exGlowScale = .5f;
                    }
                    else
                        exGlowScale *= config.FlashScale - fadeIn * (config.FlashScale - 1f);

                    beamAlpha = Utils.Clamp(((float)Math.Sin(MathHelper.ToRadians(timeSinceSpawn * 2)) + 1f) * .5f, 0f, 1f);
                    Texture2D beamTexture;
                    Texture2D glowTexture;
                    // Putting these seperately just in case...
                    if (config.DrawAdditive)
                    {
                        StartAdditive(spriteBatch);
                        beamTexture = Mod.Assets.Request<Texture2D>("Beams/SimpleBeamAdditive").Value;
                        spriteBatch.Draw(beamTexture, screenCenter - new Vector2(0, itemFrameHeight * .5f + 56 * exScale), null, beamColor * fadeIn * (.75f + beamAlpha * .25f) * config.BeamOpacity, 0, beamTexture.Size() * 0.5f, exScale, SpriteEffects.None, 0);
                        beamTexture = Mod.Assets.Request<Texture2D>("Glows/CenterAdditive").Value;
                        spriteBatch.Draw(beamTexture, screenCenter - new Vector2(0, itemFrameHeight * .5f), null, beamColor * fadeIn * (.75f + beamAlpha * .25f) * config.GlowOpacity, 0, beamTexture.Size() * 0.5f, exGlowScale, SpriteEffects.None, 0);
                        float glowScale = .3f + beamAlpha * .05f * Utils.Clamp((itemTex.Width / 16 + itemFrameHeight / 16) / 2, .25f, 5f);
                        glowTexture = Mod.Assets.Request<Texture2D>("Glows/SimpleGlowAdditive").Value;
                        spriteBatch.Draw(glowTexture, screenCenter - new Vector2(0, itemFrameHeight * .5f), null, beamColor * fadeIn * (.5f + beamAlpha * .5f) * config.GlowOpacity, 0, glowTexture.Size() * 0.5f, glowScale * exGlowScale, SpriteEffects.None, 0);
                        StopAdditive(spriteBatch);
                    }
                    else
                    {
                        beamTexture = Mod.Assets.Request<Texture2D>("Beams/SimpleBeam").Value;
                        spriteBatch.Draw(beamTexture, screenCenter - new Vector2(0, itemFrameHeight * .5f + 56 * exScale), null, beamColor * fadeIn * (.75f + beamAlpha * .25f) * config.BeamOpacity, 0, beamTexture.Size() * 0.5f, exScale, SpriteEffects.None, 0);
                        beamTexture = Mod.Assets.Request<Texture2D>("Glows/Center").Value;
                        spriteBatch.Draw(beamTexture, screenCenter - new Vector2(0, itemFrameHeight * .5f), null, beamColor * fadeIn * (.75f + beamAlpha * .25f) * config.GlowOpacity, 0, beamTexture.Size() * 0.5f, exGlowScale, SpriteEffects.None, 0);
                        float glowScale = .3f + beamAlpha * .05f * Utils.Clamp((itemTex.Width / 16 + itemFrameHeight / 16) / 2, .25f, 5f);
                        glowTexture = Mod.Assets.Request<Texture2D>("Glows/SimpleGlow").Value;
                        spriteBatch.Draw(glowTexture, screenCenter - new Vector2(0, itemFrameHeight * .5f), null, beamColor * fadeIn * (.5f + beamAlpha * .5f) * config.GlowOpacity, 0, glowTexture.Size() * 0.5f, glowScale * exGlowScale, SpriteEffects.None, 0);
                    }
                    beingDrawn = true;
                }
                #endregion
                fadeIn = Utils.Clamp(fadeIn + .025f, 0f, 1f);
            }
            return base.PreDrawInWorld(item, spriteBatch, lightColor, alphaColor, ref rotation, ref scale, whoAmI);
        }
        public override bool GrabStyle(Item item, Player player)
        {
            ref LootBeamData lootBeamData = ref LootBeamSystem.lootBeamDataByIndex[item.whoAmI];
            ref float fadeIn = ref lootBeamData.fadeIn;
            fadeIn = Utils.Clamp(fadeIn - .125f, 0f, 1f);
            return base.GrabStyle(item, player);
        }
        public override void PostDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
        {
            //All ref as we are modifying persistent data
            ref LootBeamData lootBeamData = ref LootBeamSystem.lootBeamDataByIndex[whoAmI];

            if (!lootBeamData.init)
                return;

            ref int timeSinceSpawn = ref lootBeamData.timeSinceSpawn;

            ref bool beingDrawn = ref lootBeamData.beingDrawn;

            if (beingDrawn)
                timeSinceSpawn++;
        }

        private static void StartAdditive(SpriteBatch spriteBatch)
        {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static void StopAdditive(SpriteBatch spriteBatch)
        {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        }
    }

    public class LootBeamConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;
        public static LootBeamConfig Instance;

        public Dictionary<ItemDefinition, Color> ColorOverrides = new Dictionary<ItemDefinition, Color>
            {
                { new ItemDefinition(ItemID.CopperCoin), new Color(.95f, .55f, .4f, 1f) },
                { new ItemDefinition(ItemID.SilverCoin), new Color(.7f, .75f, .75f, 1f) },
                { new ItemDefinition(ItemID.GoldCoin), new Color(.875f, .8f, .35f, 1f) },
                { new ItemDefinition(ItemID.PlatinumCoin), new Color(.7f, .85f, .85f, 1f) },
                { new ItemDefinition(ItemID.DD2EnergyCrystal), new Color(0f, 1f, .55f, 1f) },
                { new ItemDefinition(ItemID.Heart), new Color(.85f, .08f, .08f, 1f) },
                { new ItemDefinition(ItemID.CandyApple), new Color(.85f, .08f, .08f, 1f) },
                { new ItemDefinition(ItemID.CandyCane), new Color(.85f, .08f, .08f, 1f) },
                { new ItemDefinition(ItemID.Star), new Color(.25f, .4f, .9f, 1f) },
                { new ItemDefinition(ItemID.SoulCake), new Color(.25f, .4f, .9f, 1f) },
                { new ItemDefinition(ItemID.SugarPlum), new Color(.25f, .4f, .9f, 1f) },
                { new ItemDefinition(ItemID.ManaCloakStar), new Color(.25f, .4f, .9f, 1f) },
                { new ItemDefinition(ItemID.NebulaPickup1), new Color(1f, 0f, 1f, 1f) },
                { new ItemDefinition(ItemID.NebulaPickup2), new Color(.8f, .4f, .1f, 1f) },
                { new ItemDefinition(ItemID.NebulaPickup3), new Color(.5f, 0f, .5f, 1f) }
            };

        public List<ItemDefinition> CustomWhitelist = new List<ItemDefinition>
            {
                new ItemDefinition(ItemID.CopperCoin),
                new ItemDefinition(ItemID.SilverCoin),
                new ItemDefinition(ItemID.GoldCoin),
                new ItemDefinition(ItemID.PlatinumCoin),
                new ItemDefinition(ItemID.DD2EnergyCrystal),
                new ItemDefinition(ItemID.GoodieBag),
                new ItemDefinition(ItemID.Present),
                new ItemDefinition(ItemID.GoldenKey),
                new ItemDefinition(ItemID.ShadowKey),
                new ItemDefinition(ItemID.JungleKey),
                new ItemDefinition(ItemID.CorruptionKey),
                new ItemDefinition(ItemID.HallowedKey),
                new ItemDefinition(ItemID.FrozenKey),
                new ItemDefinition(ItemID.DungeonDesertKey),
                new ItemDefinition(ItemID.Vitamins),
                new ItemDefinition(ItemID.AdhesiveBandage),
                new ItemDefinition(ItemID.Nazar),
                new ItemDefinition(ItemID.TrifoldMap),
                new ItemDefinition(ItemID.ArmorPolish),
                new ItemDefinition(ItemID.Bezoar),
                new ItemDefinition(ItemID.Megaphone),
                new ItemDefinition(ItemID.FastClock),
                new ItemDefinition(ItemID.Blindfold),
                new ItemDefinition(ItemID.RodofDiscord)
            };

        public List<ItemDefinition> CustomBlacklist = new List<ItemDefinition>
            {
                new ItemDefinition(ItemID.Heart),
                new ItemDefinition(ItemID.CandyApple),
                new ItemDefinition(ItemID.CandyCane),
                new ItemDefinition(ItemID.Star),
                new ItemDefinition(ItemID.SoulCake),
                new ItemDefinition(ItemID.SugarPlum),
                new ItemDefinition(ItemID.ManaCloakStar),
                new ItemDefinition(ItemID.NebulaPickup1),
                new ItemDefinition(ItemID.NebulaPickup2),
                new ItemDefinition(ItemID.NebulaPickup3),
                new ItemDefinition(ItemID.SoulofLight),
                new ItemDefinition(ItemID.SoulofNight),
                new ItemDefinition(ItemID.SoulofFlight),
                new ItemDefinition(ItemID.SoulofFright),
                new ItemDefinition(ItemID.SoulofMight),
                new ItemDefinition(ItemID.SoulofSight)
            };

        public List<ItemDefinition> UseMiniBeam = new List<ItemDefinition>
            {
                new ItemDefinition(ItemID.CopperCoin),
                new ItemDefinition(ItemID.SilverCoin),
                new ItemDefinition(ItemID.GoldCoin),
                new ItemDefinition(ItemID.PlatinumCoin),
                new ItemDefinition(ItemID.DD2EnergyCrystal),
                new ItemDefinition(ItemID.Heart),
                new ItemDefinition(ItemID.CandyApple),
                new ItemDefinition(ItemID.CandyCane),
                new ItemDefinition(ItemID.Star),
                new ItemDefinition(ItemID.SoulCake),
                new ItemDefinition(ItemID.SugarPlum),
                new ItemDefinition(ItemID.ManaCloakStar),
                new ItemDefinition(ItemID.NebulaPickup1),
                new ItemDefinition(ItemID.NebulaPickup2),
                new ItemDefinition(ItemID.NebulaPickup3)
            };

        //[Range(0, 3)]
        //[DefaultValue(1)]
        //public int BeamStyle { get; set; }

        //[Range(0, 1)]
        //[DefaultValue(1)]
        //public int GlowStyle { get; set; }

        [Range(-2, 11)]
        [DefaultValue(1)]
        public int MinRarity { get; set; }

        [Range(0, int.MaxValue)]
        [DefaultValue(1)]
        public int MinValue { get; set; }

        [DefaultValue(true)]
        public bool HighlightQuest { get; set; }

        [DefaultValue(true)]
        public bool HighlightExpert { get; set; }

        [DefaultValue(true)]
        public bool HighlightMaster { get; set; }

        [Range(.5f, 2f)]
        [DefaultValue(.75f)]
        public float BeamScale { get; set; }

        [Range(0f, 1f)]
        [DefaultValue(1f)]
        public float BeamOpacity { get; set; }

        [Range(.5f, 2f)]
        [DefaultValue(.75f)]
        public float GlowScale { get; set; }

        [Range(0f, 1f)]
        [DefaultValue(1f)]
        public float GlowOpacity { get; set; }

        [Range(1f, 10f)]
        [DefaultValue(7.5f)]
        public float FlashScale { get; set; }

        [DefaultValue(true)]
        public bool DrawAdditive { get; set; }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

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

    //Simplifies initialization as struct
    public struct LootBeamData
    {
        public Vector3 rarityColor;
        public Color beamColor;
        public float fadeIn = 0f;
        public float beamAlpha = 0f;
        public int beamDir = 1;

        public int type = ItemID.None;
        public bool init = false;

        public LootBeamData(Vector3 rarityColor, Color beamColor, float fadeIn, float beamAlpha, int beamDir, int type, bool init)
        {
            this.rarityColor = rarityColor;
            this.beamColor = beamColor;
            this.fadeIn = fadeIn;
            this.beamAlpha = beamAlpha;
            this.beamDir = beamDir;
            this.type = type;
            this.init = init;
        }
    }

    public class LootBeamPlayer : ModPlayer
    {
        public override void OnEnterWorld(Player player)
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
        LootBeamConfig config => ModContent.GetInstance<LootBeamConfig>();

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
            ref float beamAlpha = ref lootBeamData.beamAlpha;
            ref int beamDir = ref lootBeamData.beamDir;

            ItemDefinition itemd = new ItemDefinition(item.type);
            if (!Main.dedServ && Main.netMode != NetmodeID.Server && !config.CustomBlacklist.Contains(itemd))
            {
                beamAlpha += (float)beamDir * .0125f;
                if (beamAlpha > 1f)
                {
                    beamAlpha = 1f;
                    beamDir = -1;
                }
                if (beamAlpha < 0f)
                {
                    beamAlpha = 0f;
                    beamDir = 1;
                }

                if (config.ColorMode)
                {
                    if (item.expert || item.rare == ItemRarityID.Expert)
                        rarityColor = new Vector3(255, 106, 0);
                    else if (item.master || item.rare == ItemRarityID.Master)
                        rarityColor = new Vector3(0, 255, 144);
                    else if (!(item.expert || item.rare == ItemRarityID.Expert) && !(item.master || item.rare == ItemRarityID.Master))
                    {
                        switch (item.value)
                        {
                            case >= 500 and < 5000:
                                rarityColor = new Vector3(76, 255, 0);
                                break;
                            case >= 5000 and < 50000:
                                rarityColor = new Vector3(0, 148, 255);
                                break;
                            case >= 50000 and < 500000:
                                rarityColor = new Vector3(72, 0, 255);
                                break;
                            case >= 500000:
                                rarityColor = new Vector3(255, 106, 0);
                                break;
                            case ItemRarityID.Quest:
                                rarityColor = new Vector3(0, 255, 144);
                                break;
                            default:
                                rarityColor = new Vector3(255, 255, 255);
                                break;
                        }
                    }
                    switch (item.type)
                    {
                        case ItemID.NebulaPickup1: //Damage
                            rarityColor = new Vector3(255, 0, 255);
                            break;
                        case ItemID.NebulaPickup2: //Life
                            rarityColor = new Vector3(210, 105, 30);
                            break;
                        case ItemID.NebulaPickup3: //Mana
                            rarityColor = new Vector3(128, 0, 128);
                            break;
                        default:
                            break;
                    }
                    if (item.type == ItemID.Heart || item.type == ItemID.CandyApple || item.type == ItemID.CandyCane)
                        rarityColor = new Vector3(255, 0, 0);
                    else if (item.type == ItemID.Star || item.type == ItemID.SoulCake || item.type == ItemID.SugarPlum || item.type == ItemID.ManaCloakStar)
                        rarityColor = new Vector3(0, 0, 255);
                    else if (item.type == ItemID.CopperCoin || item.type == ItemID.SilverCoin || item.type == ItemID.GoldCoin || item.type == ItemID.PlatinumCoin)
                        rarityColor = new Vector3(255, 216, 0);
                    else if (item.type == ItemID.DD2EnergyCrystal || item.type == ItemID.DefenderMedal)
                        rarityColor = new Vector3(0, 255, 144);
                }
                else
                {
                    if (item.expert || item.rare == ItemRarityID.Expert)
                        rarityColor = Main.DiscoColor.ToVector3() * new Vector3(255);
                    else if (item.master || item.rare == ItemRarityID.Master)
                        rarityColor = new Vector3(255, Main.masterColor * 200, 0f);
                    else if (!(item.expert || item.rare == ItemRarityID.Expert) && !(item.master || item.rare == ItemRarityID.Master))
                    {
                        switch (item.rare)
                        {
                            case ItemRarityID.Gray:
                                rarityColor = new Vector3(100, 100, 100);
                                break;
                            case ItemRarityID.Blue:
                                rarityColor = new Vector3(134, 134, 229);
                                break;
                            case ItemRarityID.Green:
                                rarityColor = new Vector3(146, 248, 146);
                                break;
                            case ItemRarityID.Orange:
                                rarityColor = new Vector3(233, 182, 136);
                                break;
                            case ItemRarityID.LightRed:
                                rarityColor = new Vector3(244, 144, 144);
                                break;
                            case ItemRarityID.Pink:
                                rarityColor = new Vector3(248, 146, 248);
                                break;
                            case ItemRarityID.LightPurple:
                                rarityColor = new Vector3(190, 144, 229);
                                break;
                            case ItemRarityID.Lime:
                                rarityColor = new Vector3(140, 241, 10);
                                break;
                            case ItemRarityID.Yellow:
                                rarityColor = new Vector3(249, 249, 9);
                                break;
                            case ItemRarityID.Cyan:
                                rarityColor = new Vector3(4, 195, 249);
                                break;
                            case ItemRarityID.Red:
                                rarityColor = new Vector3(225, 6, 67);
                                break;
                            case ItemRarityID.Purple:
                                rarityColor = new Vector3(178, 39, 253);
                                break;
                            case ItemRarityID.Quest:
                                rarityColor = new Vector3(241, 165, 0);
                                break;
                            default:
                                rarityColor = new Vector3(255, 255, 255);
                                break;
                        }
                    }
                    if (LootBeams.modRarities.TryGetValue(item.rare, out string name))
                    {
                        rarityColor = ModContent.Find<ModRarity>(name).RarityColor.ToVector3() * new Vector3(255);
                    }
                    try
                    {
                        if (config.ColorOverrides.ContainsKey(itemd))
                        {
                            if (config.ColorOverrides.TryGetValue(itemd, out Color color))
                                rarityColor = color.ToVector3() * new Vector3(255);
                        }
                    }
                    catch
                    {
                        Mod.Logger.Error("[LootBeams] ItemDefinition or Color is invalid! How did this happen?");
                    };
                }
                beamColor = new Color(rarityColor.X / 255f, rarityColor.Y / 255f, rarityColor.Z / 255f);
                float glowScale = .3f + beamAlpha * .05f * Utils.Clamp((item.width / 16 + item.height / 16) / 2, .25f, 5f);
                if (config.CompatibilityMode)
                    glowScale = .3f + .05f * Utils.Clamp((item.width / 16 + item.height / 16) / 2, .25f, 5f);
                Vector2 screenCenter = new Vector2(
                    item.Center.X - Main.screenPosition.X,
                    item.Center.Y - Main.screenPosition.Y
                    );
                if (config.CompatibilityMode)
                {
                    if (config.CustomWhitelist.Contains(itemd) || (config.HighlightQuest && (item.questItem || item.rare == ItemRarityID.Quest)) || (config.HighlightExpert && (item.expert || item.rare == ItemRarityID.Expert)) || (config.HighlightMaster && (item.master || item.rare == ItemRarityID.Master)))
                    {
                        if (config.UseMiniBeam.Contains(itemd))
                        {
                            Texture2D texture = Mod.Assets.Request<Texture2D>("LootBeam").Value;
                            spriteBatch.Draw(texture, screenCenter - new Vector2(0, 28), null, beamColor * config.BeamOpacity, 0, texture.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootCenter").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * config.GlowOpacity, 0, texture.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootGlow").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * config.GlowOpacity, 0, texture.Size() * 0.5f, glowScale * 0.5f, SpriteEffects.None, 0);
                        }
                        else
                        {
                            float exScale = config.BeamScale;
                            float exGlowScale = config.GlowScale;
                            Texture2D texture = Mod.Assets.Request<Texture2D>("LootBeam").Value;
                            spriteBatch.Draw(texture, screenCenter - new Vector2(0, 56 * exScale), null, beamColor * config.BeamOpacity, 0, texture.Size() * 0.5f, exScale, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootCenter").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * config.GlowOpacity, 0, texture.Size() * 0.5f, exGlowScale, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootGlow").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * config.GlowOpacity, 0, texture.Size() * 0.5f, glowScale * exGlowScale, SpriteEffects.None, 0);
                        }
                    }
                    else if (((config.MinRarity > -2 && item.rare >= config.MinRarity && item.value >= config.MinValue) || config.ColorMode) && !(item.questItem || item.rare == ItemRarityID.Quest) && !(item.expert || item.rare == ItemRarityID.Expert) && !(item.master || item.rare == ItemRarityID.Master))
                    {
                        if (config.UseMiniBeam.Contains(itemd))
                        {
                            Texture2D texture = Mod.Assets.Request<Texture2D>("LootBeam").Value;
                            spriteBatch.Draw(texture, screenCenter - new Vector2(0, 28), null, beamColor * config.BeamOpacity, 0, texture.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootCenter").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * config.GlowOpacity, 0, texture.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootGlow").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * config.GlowOpacity, 0, texture.Size() * 0.5f, glowScale * 0.5f, SpriteEffects.None, 0);
                        }
                        else
                        {
                            float exScale = config.BeamScale;
                            float exGlowScale = config.GlowScale;
                            Texture2D texture = Mod.Assets.Request<Texture2D>("LootBeam").Value;
                            spriteBatch.Draw(texture, screenCenter - new Vector2(0, 56 * exScale), null, beamColor * config.BeamOpacity, 0, texture.Size() * 0.5f, exScale, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootCenter").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * config.GlowOpacity, 0, texture.Size() * 0.5f, exGlowScale, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootGlow").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * config.GlowOpacity, 0, texture.Size() * 0.5f, glowScale * exGlowScale, SpriteEffects.None, 0);
                        }
                    }
                }
                else
                {
                    if (config.CustomWhitelist.Contains(itemd) || (config.HighlightQuest && (item.questItem || item.rare == ItemRarityID.Quest)) || (config.HighlightExpert && (item.expert || item.rare == ItemRarityID.Expert)) || (config.HighlightMaster && (item.master || item.rare == ItemRarityID.Master)))
                    {
                        if (config.UseMiniBeam.Contains(itemd))
                        {
                            Texture2D texture = Mod.Assets.Request<Texture2D>("LootBeam").Value;
                            spriteBatch.Draw(texture, screenCenter - new Vector2(0, 28), null, beamColor * fadeIn * (.75f + beamAlpha * .25f) * config.BeamOpacity, 0, texture.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootCenter").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * fadeIn * (.75f + beamAlpha * .25f) * config.GlowOpacity, 0, texture.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootGlow").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * fadeIn * (.5f + beamAlpha * .5f) * config.GlowOpacity, 0, texture.Size() * 0.5f, glowScale * 0.5f, SpriteEffects.None, 0);
                        }
                        else
                        {
                            float exScale = config.BeamScale;
                            float exGlowScale = config.GlowScale;
                            Texture2D texture = Mod.Assets.Request<Texture2D>("LootBeam").Value;
                            spriteBatch.Draw(texture, screenCenter - new Vector2(0, 56 * exScale), null, beamColor * fadeIn * (.75f + beamAlpha * .25f) * config.BeamOpacity, 0, texture.Size() * 0.5f, exScale, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootCenter").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * fadeIn * (.75f + beamAlpha * .25f) * config.GlowOpacity, 0, texture.Size() * 0.5f, exGlowScale, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootGlow").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * fadeIn * (.5f + beamAlpha * .5f) * config.GlowOpacity, 0, texture.Size() * 0.5f, glowScale * exGlowScale, SpriteEffects.None, 0);
                        }
                    }
                    else if (((config.MinRarity > -2 && item.rare >= config.MinRarity && (item.value >= config.MinValue || item.type == ItemID.Present)) || config.ColorMode) && !(item.questItem || item.rare == ItemRarityID.Quest) && !(item.expert || item.rare == ItemRarityID.Expert) && !(item.master || item.rare == ItemRarityID.Master))
                    {
                        if (config.UseMiniBeam.Contains(itemd))
                        {
                            Texture2D texture = Mod.Assets.Request<Texture2D>("LootBeam").Value;
                            spriteBatch.Draw(texture, screenCenter - new Vector2(0, 28), null, beamColor * fadeIn * (.75f + beamAlpha * .25f) * config.BeamOpacity, 0, texture.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootCenter").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * fadeIn * (.75f + beamAlpha * .25f) * config.GlowOpacity, 0, texture.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootGlow").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * fadeIn * (.5f + beamAlpha * .5f) * config.GlowOpacity, 0, texture.Size() * 0.5f, glowScale * 0.5f, SpriteEffects.None, 0);
                        }
                        else
                        {
                            float exScale = config.BeamScale;
                            float exGlowScale = config.GlowScale;
                            Texture2D texture = Mod.Assets.Request<Texture2D>("LootBeam").Value;
                            spriteBatch.Draw(texture, screenCenter - new Vector2(0, 56 * exScale), null, beamColor * fadeIn * (.75f + beamAlpha * .25f) * config.BeamOpacity, 0, texture.Size() * 0.5f, exScale, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootCenter").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * fadeIn * (.75f + beamAlpha * .25f) * config.GlowOpacity, 0, texture.Size() * 0.5f, exGlowScale, SpriteEffects.None, 0);
                            texture = Mod.Assets.Request<Texture2D>("LootGlow").Value;
                            spriteBatch.Draw(texture, screenCenter, null, beamColor * fadeIn * (.5f + beamAlpha * .5f) * config.GlowOpacity, 0, texture.Size() * 0.5f, glowScale * exGlowScale, SpriteEffects.None, 0);
                        }
                    }
                }

                if (fadeIn < 1f)
                    fadeIn += .0125f;
                else if (fadeIn > 1f)
                    fadeIn = 1f;
            }
            return base.PreDrawInWorld(item, spriteBatch, lightColor, alphaColor, ref rotation, ref scale, whoAmI);
        }
    }

    [Label("Config")]
    public class LootBeamConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;
        public static LootBeamConfig Instance;

        [Label("Color Overrides")]
        [Tooltip("Allows customizing loot beam colors for specific items.\n" +
            "Currently does not support rarity-wide color definitions.\n" +
            "Note: Alpha value goes unused, but should be set to 255 for an accurate color preview.")]
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

        [Label("Custom Whitelist")]
        [Tooltip("These items will ALWAYS have a loot beam regardless of conditions.")]
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

        [Label("Custom Blacklist")]
        [Tooltip("These items will NEVER have a loot beam regardless of conditions.")]
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

        [Label("Minimized Beams")]
        [Tooltip("These items will have a set loot beam size of 0.5, mainly used for currencies and/or to reduce distraction.\n" +
            "Opacity affects beam and glow individually as normal.")]
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

        [Label("Minimum Rarity")]
        [Tooltip("The minimum rarity for an item to have a loot beam?\n" +
            "Setting this to -2 will disable the loot beams entirely from non-overriden items. (Check Custom Overrides)\n" +
            "Range: -2 to 11\n" +
            "Default: 1")]
        [Range(-2, 11)]
        [DefaultValue(1)]
        public int MinRarity { get; set; }

        [Label("Minimum Value")]
        [Tooltip("How valuable in copper coins an item must be to have a loot beam?\n" +
            "Remember: A silver coin is equal to 100 copper, a gold coin is equal to 10,000 copper,\n" +
            "and a platinum coin is equal to 1,000,000 copper.\n" +
            "Another important distinction is that this is for BUY value, not sell value,\n" +
            "so be sure to refer to any relevant wiki to find an item's purchase price.\n" +
            "Range: More than or equal to 0\n" +
            "Default: 1")]
        [Range(0, int.MaxValue)]
        [DefaultValue(1)]
        public int MinValue { get; set; }

        [Label("Always Highlight Quest Items")]
        [Tooltip("Self-explanatory.\n" +
            "Overrides Minimum Value.\n" +
            "Default: On")]
        [DefaultValue(true)]
        public bool HighlightQuest { get; set; }

        [Label("Always Highlight Expert Items")]
        [Tooltip("Self-explanatory.\n" +
            "Overrides Minimum Value.\n" +
            "Default: On")]
        [DefaultValue(true)]
        public bool HighlightExpert { get; set; }

        [Label("Always Highlight Master Items")]
        [Tooltip("Self-explanatory.\n" +
            "Overrides Minimum Value.\n" +
            "Default: On")]
        [DefaultValue(true)]
        public bool HighlightMaster { get; set; }

        [Label("Beam Scale")]
        [Tooltip("Self-explanatory.\n" +
            "Items defined in the Minimized Beams list always have a scale of 0.5\n" +
            "Range: 0.5 to 2.0\n" +
            "Default: 0.75")]
        [Range(.5f, 2f)]
        [DefaultValue(.75f)]
        public float BeamScale { get; set; }

        [Label("Beam Opacity")]
        [Tooltip("Self-explanatory.\n" +
            "Range: 0.0 to 1.0\n" +
            "Default: 1.0")]
        [Range(0f, 1f)]
        [DefaultValue(1f)]
        public float BeamOpacity { get; set; }

        [Label("Glow Scale")]
        [Tooltip("Self-explanatory.\n" +
            "Items defined in the Minimized Beams list always have a scale of 0.5\n" +
            "Range: 0.5 to 2.0\n" +
            "Default: 0.75")]
        [Range(.5f, 2f)]
        [DefaultValue(.75f)]
        public float GlowScale { get; set; }

        [Label("Glow Opacity")]
        [Tooltip("Self-explanatory.\n" +
            "Range: 0.0 to 1.0\n" +
            "Default: 1.0")]
        [Range(0f, 1f)]
        [DefaultValue(1f)]
        public float GlowOpacity { get; set; }

        [Label("Alternate Color Mode")]
        [Tooltip("Changes the colors to match that of Borderlands.\n" +
            "Beam colors are dependant on the item's buy price (refer to Minimum Value's tooltip for conversion):\n" +
            "No Value to 5 silver = White, 5 silver to 50 silver = Green\n" +
            "50 silver to 5 gold = Blue, 5 gold to 50 gold = Purple\n" +
            "50 gold and above = Orange\n" +
            "Expert items are always orange (Legendary), whereas Quest and Master items are always seafoam green (Pearlescent)\n" +
            "Coins are also always yellow and defender medals are always seafoam green.\n" +
            "This alternate color mode will override most other config options, though blacklists and scale/opacity configuration will still apply.\n" +
            "Default: Off")]
        [DefaultValue(false)]
        public bool ColorMode { get; set; }

        [Label("Use Simplified Rendering")]
        [Tooltip("Disables dynamic rendering in favor of a simpler implementation\n" +
            "In short this means that the beams will not fade in, and the glow effect will not pulse\n" +
            "Scale and opacity configuration options will still apply\n" +
            "Default: Off")]
        [DefaultValue(false)]
        public bool CompatibilityMode { get; set; }
    }
}
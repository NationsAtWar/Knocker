﻿using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

/*  TO DO
 *  
 *  Clean up code / Add comments
 *  Add different texture for each variation of the knocker
 */

namespace AculemMods {

    public class KnockerMod : ModSystem {

        public override void Start(ICoreAPI api) {

            base.Start(api);
            api.RegisterItemClass("Knocker", typeof(KnockerItem));
        }
    }

    public class KnockerItem : Item
    {
        private const int maxKnocks = 5;
        private const float speed = 60f;
        private const float knockDuration = 0.4f;

        private bool reachedKnock = false;
        private bool finishedKnocking = false;
        private int numberOfKnocks = 0;
        private BlockPos selectedBlock;

        private enum IntensityLevel
        {
            None,
            Minute,
            VeryLow,
            Low,
            Medium,
            High,
            VeryHigh,
            UltraHigh
        }

        private enum OreType
        {
            nativecopper,
            limonite,
            nativegold,
            galena,
            cassiterite,
            chromite,
            ilmenite,
            sphalerite,
            silver,
            bismuthinite,
            magnetite,
            hematite,
            malachite,
            pentlandite,
            uranium,
            rhodocrocite
        }

        public static SimpleParticleProperties particles = new SimpleParticleProperties(
                    1, 1,
                    ColorUtil.ToRgba(50, 220, 220, 220),
                    new Vec3d(),
                    new Vec3d(),
                    new Vec3f(-0.25f, 0.1f, -0.25f),
                    new Vec3f(0.25f, 0.1f, 0.25f),
                    1.5f,
                    -0.075f,
                    0.25f,
                    0.25f,
                    EnumParticleModel.Quad
                );

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling) {

            Block block = byEntity.Api.World.BlockAccessor.GetBlock(blockSel.Position);

            if (block.BlockMaterial.Equals(EnumBlockMaterial.Stone))
                handling = EnumHandHandling.PreventDefaultAnimation;
            else {

                handling = EnumHandHandling.Handled;
                finishedKnocking = true;
            }
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel) {

            numberOfKnocks = 0;
            finishedKnocking = false;
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel) {

            if (finishedKnocking || blockSel == null)
                return true;

            if (byEntity.World is IClientWorldAccessor) {

                // byEntity.Api.Logger.Debug("" + blockSel.);

                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float knockStep = secondsUsed % knockDuration;

                tf.Origin.Set(0, -1, 0);
                tf.Rotation.Z = Math.Min(30, knockStep * speed);

                if (knockStep > knockDuration - 0.1f) {

                    // First knock determines which block you're selecting to knock
                    if (numberOfKnocks == 0) {

                        selectedBlock = blockSel.Position;
                    } else { // On subsequent knocks, check to see if the block being knocked is the same. If not, reset the knocking process

                        if (selectedBlock != blockSel.Position) {

                            selectedBlock = blockSel.Position;
                            numberOfKnocks = 1;
                        }
                    }

                    // Executes once per knock
                    if (!reachedKnock) {

                        IPlayer byPlayer = (byEntity as EntityPlayer).Player;

                        reachedKnock = true;
                        numberOfKnocks += 1;

                        // Once max knocks is reached, execute this code
                        if (numberOfKnocks >= maxKnocks) {

                            ICoreClientAPI clientAPI = (ICoreClientAPI)byEntity.Api;

                            numberOfKnocks = 0;
                            finishedKnocking = true;
                            tf.Rotation.Z = 0;
                            byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/chop1"), byPlayer, byPlayer);
                            SpawnParticle(10, byEntity);

                            BlockPos plrpos = byPlayer.Entity.Pos.AsBlockPos;
                            float oreDensity = 0;

                            int maxDistance = 32;
                            string oreType = GetOreType(slot.Itemstack.Item.Code.GetName());

                            // Searches the radius around the selected block, returning an 'intensity' dependent on how many blocks it finds and how close they are
                            api.World.BlockAccessor.WalkBlocks(
                                plrpos.AddCopy(-maxDistance, -maxDistance, -maxDistance),
                                plrpos.AddCopy(maxDistance, maxDistance, maxDistance),
                                (block, pos) => oreDensity += (block.BlockMaterial.Equals(EnumBlockMaterial.Ore) && block.LastCodePart(1).ToString().Contains(oreType)) ? 1 / pos.DistanceTo(blockSel.Position) : 0
                            );

                            // (block, pos) => oreDensity += (!block.Code.Path.Contains("quartz") && block.BlockMaterial.Equals(EnumBlockMaterial.Ore)) ? 1 / pos.DistanceTo(blockSel.Position) : 0

                            /* Look into this method later, could be more efficient
                            api.World.BlockAccessor.SearchBlocks(
                                plrpos.AddCopy(-64, -64, -64),
                                plrpos.AddCopy(64, 64, 64),
                                (block, pos) => quantityLogs += !block.BlockMaterial.Equals(EnumBlockMaterial.Ore) ? 0 : 1
                            );
                            */

                            // Reduced the durability of the Knocker by 1
                            int durability = slot.Itemstack.Attributes.GetInt("durability", 5000);
                            durability -= 1;
                            slot.Itemstack.Attributes.SetInt("durability", durability);

                            float correctOreDensity = CorrectOreDensity(oreType, oreDensity);

                            clientAPI.ShowChatMessage(slot.Itemstack.GetName());
                            clientAPI.ShowChatMessage("Ore Intensity: " + GetIntensityLevel(correctOreDensity) + " (" + correctOreDensity + ")");

                        } else {

                            byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/chop3"), byPlayer, byPlayer);
                            SpawnParticle(1, byEntity);
                        }
                    }
                } else
                    reachedKnock = false;

                byEntity.Controls.UsingHeldItemTransformAfter = tf;
            }
            return true;
        }

        private void SpawnParticle(int amount, EntityAgent entity) {

            for (int i = 0; i < amount; i++) {

                Vec3d pos =
                        entity.Pos.XYZ.Add(0, entity.LocalEyePos.Y, 0)
                        .Ahead(1f, entity.Pos.Pitch, entity.Pos.Yaw)
                    ;

                Vec3f speedVec = new Vec3d(0, 0, 0).Ahead(5, entity.Pos.Pitch, entity.Pos.Yaw).ToVec3f();
                particles.MinVelocity = speedVec;
                Random rand = new Random();
                particles.Color = ColorUtil.ToRgba(255, rand.Next(50, 150), rand.Next(50, 150), rand.Next(150, 255));
                particles.MinPos = pos.AddCopy(-0.05, -0.05, -0.05);
                particles.AddPos.Set(0.1, 0.1, 0.1);
                particles.MinSize = 0.1F;
                particles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.SINUS, 10);
                entity.World.SpawnParticles(particles);
            }
        }

        private float CorrectOreDensity(string oreType, float oreDensity) {

            OreType enumOreType = (OreType)Enum.Parse(typeof(OreType), oreType);

            int intensityBase = 1;

            if (enumOreType == OreType.bismuthinite)
                intensityBase = 4;
            else if (enumOreType == OreType.cassiterite)
                intensityBase = 5;
            else if (enumOreType == OreType.chromite)
                intensityBase = 3;
            else if (enumOreType == OreType.galena)
                intensityBase = 4;
            else if (enumOreType == OreType.hematite)
                intensityBase = 26;
            else if (enumOreType == OreType.ilmenite)
                intensityBase = 3;
            else if (enumOreType == OreType.limonite)
                intensityBase = 26;
            else if (enumOreType == OreType.magnetite)
                intensityBase = 26;
            else if (enumOreType == OreType.malachite)
                intensityBase = 6;
            else if (enumOreType == OreType.nativecopper)
                intensityBase = 6;
            else if (enumOreType == OreType.nativegold)
                intensityBase = 4;
            else if (enumOreType == OreType.pentlandite)
                intensityBase = 6;
            else if (enumOreType == OreType.rhodocrocite)
                intensityBase = 3;
            else if (enumOreType == OreType.silver)
                intensityBase = 4;
            else if (enumOreType == OreType.sphalerite)
                intensityBase = 4;
            else if (enumOreType == OreType.uranium)
                intensityBase = 4;

            return oreDensity / intensityBase;
        }

        private IntensityLevel GetIntensityLevel(float intensityInt) {

            if (intensityInt == 0)
                return IntensityLevel.None;
            else if (intensityInt > 0 && intensityInt < 0.25f)
                return IntensityLevel.Minute;
            else if (intensityInt >= 0.25f && intensityInt < 0.5f)
                return IntensityLevel.VeryLow;
            else if (intensityInt >= 0.5f && intensityInt < 0.75f)
                return IntensityLevel.Low;
            else if (intensityInt >= 0.75f && intensityInt < 1.0f)
                return IntensityLevel.Medium;
            else if (intensityInt >= 1.0f && intensityInt < 2.0f)
                return IntensityLevel.High;
            else if (intensityInt > 2.0f && intensityInt < 3.0f)
                return IntensityLevel.VeryHigh;
            else if (intensityInt >= 3.0f)
                return IntensityLevel.UltraHigh;
            else
                return IntensityLevel.None;
        }

        private string GetOreType(string itemName) {

            foreach (string oreType in Enum.GetNames(typeof(OreType))) {

                if (itemName.Contains(oreType))
                    return oreType;
            }

            return null;
        }
    }
}
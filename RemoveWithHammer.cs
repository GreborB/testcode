using Oxide.Core;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("RemoveWithHammer", "Kinas Playground", "1.3.1")]
    [Description("Removes building blocks with Shift+R while holding a hammer. /remove all toggles mass removal mode.")]

    public class RemoveWithHammer : RustPlugin
    {
        #region Configuration

        private PluginConfig config;

        private class PluginConfig
        {
            public bool RequireOwnership { get; set; } = true;
            public bool DefaultMassRemove { get; set; } = false;
            public float MaxDistance { get; set; } = 8f;
            public float SphereRadius { get; set; } = 0.25f;
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            if (config.MaxDistance <= 0f) config.MaxDistance = 8f;
            if (config.SphereRadius <= 0f) config.SphereRadius = 0.25f;
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data

        private readonly Dictionary<ulong, bool> massRemoveEnabled = new();

        private bool IsMassRemoveEnabled(BasePlayer player)
        {
            return massRemoveEnabled.TryGetValue(player.userID, out var enabled) ? enabled : config.DefaultMassRemove;
        }

        private void ToggleMassRemove(BasePlayer player)
        {
            bool current = IsMassRemoveEnabled(player);
            bool newState = !current;
            massRemoveEnabled[player.userID] = newState;

            if (newState)
            {
                ShowRemoveAllUI(player);
                player.ChatMessage("Mass removal mode is now enabled.");
            }
            else
            {
                DestroyRemoveAllUI(player);
                player.ChatMessage("Mass removal mode is now disabled.");
            }
        }

        #endregion

        #region Hooks

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!input.WasJustPressed(BUTTON.RELOAD)) return;
            if (!input.IsDown(BUTTON.SPRINT)) return;
            if (player == null || !player.IsConnected) return;

            var heldItem = player.GetActiveItem();
            if (heldItem == null || !heldItem.info.shortname.Contains("hammer")) return;

            var target = FindTargetEntity(player);
            if (target == null)
            {
                player.ChatMessage("You're not looking at a removable object.");
                return;
            }

            if (config.RequireOwnership && target.OwnerID != player.userID)
            {
                player.ChatMessage(target is BuildingBlock
                    ? "You don't own this building block."
                    : "You don't own this deployable.");
                return;
            }

            if (target is BuildingBlock block)
            {
                if (IsMassRemoveEnabled(player))
                {
                    RemoveConnectedBlocks(block, player);
                }
                else
                {
                    block.Kill();
                 //   player.ChatMessage("Building block removed.");
                }
                return;
            }

            if (target is BaseEntity deployable)
            {
                deployable.Kill();
              //  player.ChatMessage($"Removed deployable: {deployable.ShortPrefabName}");
                return;
            }

            player.ChatMessage("You're not looking at a removable object.");
        }

        #endregion

        #region Commands

        [ChatCommand("remove")]
        private void CmdRemove(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0 || args[0].ToLower() != "all")
            {
                player.ChatMessage("Usage: /remove all");
                return;
            }

            ToggleMassRemove(player);
        }

        #endregion

        #region Core Logic

        private BaseEntity FindTargetEntity(BasePlayer player)
        {
            var ray = player.eyes.HeadRay();
            float maxDist = config.MaxDistance;
            int mask = Rust.Layers.Mask.Default | Rust.Layers.Mask.Deployed | Rust.Layers.Mask.Construction;

            var hits = Physics.RaycastAll(ray, maxDist, mask).OrderBy(h => h.distance);
            foreach (var hit in hits)
            {
                var ent = hit.GetEntity() ?? hit.collider?.GetComponentInParent<BaseEntity>();
                if (IsValidRemovable(ent)) return ent;
            }

            var sphereHits = Physics.SphereCastAll(ray, config.SphereRadius, maxDist, mask).OrderBy(h => h.distance);
            foreach (var sh in sphereHits)
            {
                var ent = sh.collider?.GetComponentInParent<BaseEntity>();
                if (IsValidRemovable(ent)) return ent;
            }

            var block = GetLookAtBlock(player);
            if (block != null && IsValidRemovable(block)) return block;

            return null;
        }

        private bool IsValidRemovable(BaseEntity ent)
        {
            if (ent == null) return false;
            if (ent is BasePlayer || ent is BaseVehicle || ent is WorldItem || ent is BaseCorpse) return false;
            return true;
        }

        private BuildingBlock GetLookAtBlock(BasePlayer player)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, config.MaxDistance))
            {
                return hit.collider?.GetComponentInParent<BuildingBlock>();
            }
            return null;
        }

        private void RemoveConnectedBlocks(BuildingBlock startBlock, BasePlayer player)
        {
            var visited = new HashSet<BuildingBlock>();
            var queue = new Queue<BuildingBlock>();
            queue.Enqueue(startBlock);
            visited.Add(startBlock);

            int count = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == null || current.IsDestroyed) continue;
                if (config.RequireOwnership && current.OwnerID != player.userID) continue;

                current.Kill();
                count++;

                var nearby = new List<BaseEntity>();
                Vis.Entities(current.transform.position, 3f, nearby, Rust.Layers.Mask.Construction);

                foreach (var entity in nearby)
                {
                    var block = entity as BuildingBlock;
                    if (block != null && !visited.Contains(block))
                    {
                        visited.Add(block);
                        queue.Enqueue(block);
                    }
                }
            }

            player.ChatMessage($"Removed {count} connected building blocks.");
        }

        #endregion

        #region UI

        private void ShowRemoveAllUI(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, "RemoveAllNotice");

			var container = new CuiElementContainer();

			// Background panel
			container.Add(new CuiElement
			{
				Name = "RemoveAllNotice",
				Parent = "Overlay",
				Components =
				{
					new CuiImageComponent
					{
						Color = "1 1 1 0.6" // Semi-transparent white
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0.4 0.48",
						AnchorMax = "0.6 0.52"
					}
				}
			});

			// Text overlay
			container.Add(new CuiElement
			{
				Name = "RemoveAllNoticeText",
				Parent = "RemoveAllNotice",
				Components =
				{
					new CuiTextComponent
					{
						Text = "REMOVE ALL IS ACTIVE",
						FontSize = 24,
						Align = TextAnchor.MiddleCenter,
						Color = "1 0 0 1" // Red text
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0",
						AnchorMax = "1 1"
					}
				}
			});

			CuiHelper.AddUi(player, container);
		}

        private void DestroyRemoveAllUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "RemoveAllNotice");
			CuiHelper.DestroyUi(player, "RemoveAllNoticeText");
        }

        #endregion
    }
}

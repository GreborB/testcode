using Oxide.Core;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("RemoveWithHammer", "Kinas Playground", "1.2.0")]
    [Description("Removes building blocks with Shift+R while holding a hammer. /remove all toggles mass removal mode.")]

    public class RemoveWithHammer : RustPlugin
    {
        #region Configuration

        private PluginConfig config;

        private class PluginConfig
        {
            public bool RequireOwnership { get; set; } = true;
            public bool DefaultMassRemove { get; set; } = false;
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
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

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
            massRemoveEnabled[player.userID] = !current;
            player.ChatMessage($"Mass removal mode is now {(massRemoveEnabled[player.userID] ? "enabled" : "disabled")}.");
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

			RaycastHit hit;
			if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 8f)) return;

			var entity = hit.GetEntity();
			if (entity == null || entity is BasePlayer || entity is BaseVehicle)
			{
				player.ChatMessage("You're not looking at a removable object.");
				return;
			}

			// Building block removal
			if (entity is BuildingBlock block)
			{
				if (config.RequireOwnership && block.OwnerID != player.userID)
				{
					player.ChatMessage("You don't own this building block.");
					return;
				}

				if (IsMassRemoveEnabled(player))
				{
					RemoveConnectedBlocks(block, player);
				}
				else
				{
					block.Kill();
					player.ChatMessage("Building block removed.");
				}
				return;
			}

			// Deployable removal
			if (entity is BaseEntity deployable)
			{
				if (config.RequireOwnership && deployable.OwnerID != player.userID)
				{
					player.ChatMessage("You don't own this deployable.");
					return;
				}

				deployable.Kill();
				player.ChatMessage($"Removed deployable: {deployable.ShortPrefabName}");
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

        private BuildingBlock GetLookAtBlock(BasePlayer player)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
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
    }
}

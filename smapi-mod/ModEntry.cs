using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Characters;

namespace StardewMCPBridge
{
    public class ModEntry : Mod
    {
        private string bridgePath;
        private string actionPath;
        private BotManager botManager;

        private readonly Dictionary<string, Texture2D> portraits = new();
        private readonly Dictionary<string, Texture2D> sprites = new();

        internal static readonly HashSet<string> RegisteredCompanions = new();

        private Texture2D fallbackPortrait;
        private Texture2D fallbackSprite;

        public override void Entry(IModHelper helper)
        {
            this.botManager = new BotManager(this.Monitor, helper);
            this.bridgePath = Path.Combine(helper.DirectoryPath, "bridge_data.json");
            this.actionPath = Path.Combine(helper.DirectoryPath, "actions.json");

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;

            this.Monitor.Log("Stardew MCP Bridge initialized. Content pipeline registered.", LogLevel.Debug);
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            this.fallbackPortrait = this.Helper.ModContent.Load<Texture2D>("assets/Companion1_portrait.png");
            this.fallbackSprite = this.Helper.ModContent.Load<Texture2D>("assets/Companion1_sprite.png");

            string assetsDir = Path.Combine(this.Helper.DirectoryPath, "assets");
            if (Directory.Exists(assetsDir))
            {
                foreach (var file in Directory.GetFiles(assetsDir, "*_portrait.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(file).Replace("_portrait", "");
                    this.portraits[name] = this.Helper.ModContent.Load<Texture2D>($"assets/{name}_portrait.png");
                    this.Monitor.Log($"Loaded portrait for {name}", LogLevel.Debug);
                }
                foreach (var file in Directory.GetFiles(assetsDir, "*_sprite.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(file).Replace("_sprite", "");
                    this.sprites[name] = this.Helper.ModContent.Load<Texture2D>($"assets/{name}_sprite.png");
                    this.Monitor.Log($"Loaded sprite for {name}", LogLevel.Debug);
                }
            }

            this.Monitor.Log("Bridge online. Portraits and sprites loaded. Waiting for world.", LogLevel.Info);
        }

        internal Texture2D GetPortrait(string name)
        {
            return this.portraits.TryGetValue(name, out var tex) ? tex : this.fallbackPortrait;
        }

        internal Texture2D GetSprite(string name)
        {
            return this.sprites.TryGetValue(name, out var tex) ? tex : this.fallbackSprite;
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.BaseName.StartsWith("Portraits/Companion"))
            {
                string name = e.NameWithoutLocale.BaseName.Replace("Portraits/", "");
                e.LoadFrom(() => this.GetPortrait(name), AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.BaseName.StartsWith("Characters/Companion"))
            {
                string name = e.NameWithoutLocale.BaseName.Replace("Characters/", "");
                e.LoadFrom(() => this.GetSprite(name), AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Characters"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, CharacterData>();
                    foreach (string name in RegisteredCompanions)
                    {
                        if (!data.Data.ContainsKey(name))
                        {
                            data.Data[name] = new CharacterData
                            {
                                DisplayName = name,
                                HomeRegion = "Town",
                            };
                        }
                    }
                });
            }
        }

        internal void RegisterCompanion(string name)
        {
            if (RegisteredCompanions.Add(name))
            {
                this.Helper.GameContent.InvalidateCache("Data/Characters");
                this.Helper.GameContent.InvalidateCache($"Portraits/{name}");
                this.Helper.GameContent.InvalidateCache($"Characters/{name}");
                this.Monitor.Log($"Registered companion {name} (total: {RegisteredCompanions.Count})", LogLevel.Info);
            }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            this.botManager.Update();
            if (e.IsMultipleOf(30))
            {
                this.SyncGameState();
                this.ProcessActions();
            }
        }

        private void OnTimeChanged(object sender, TimeChangedEventArgs e)
        {
            if (e.NewTime >= 2600)
                this.botManager.SignalAllSleepReady();
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            this.botManager.SignalAllSleepReady();
            this.Monitor.Log("Day ending: bot farmers signaled sleep ready", LogLevel.Debug);
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            this.botManager.OnDayStarted();
            this.Monitor.Log("New day: companion stamina restored", LogLevel.Info);
        }

        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            this.botManager.Cleanup();
            this.Monitor.Log("Returned to title: companions cleaned up", LogLevel.Info);
        }

        private void SyncGameState()
        {
            try
            {
                var state = new
                {
                    time = Game1.timeOfDay,
                    day = Game1.dayOfMonth,
                    season = Game1.currentSeason,
                    weather = Game1.isLightning ? "storm" : Game1.isRaining ? "rain" : Game1.isSnowing ? "snow" : Game1.isDebrisWeather ? "windy" : "sunny",
                    location = Game1.currentLocation?.Name,
                    player = new
                    {
                        name = Game1.player.Name,
                        health = Game1.player.health,
                        stamina = Game1.player.Stamina,
                        money = Game1.player.Money,
                        position = new { x = Game1.player.Position.X, y = Game1.player.Position.Y }
                    },
                    companions = this.botManager.GetBotStatus(),
                    npcs = Game1.currentLocation?.characters.Select(c => new {
                        name = c.Name,
                        position = new { x = c.Position.X, y = c.Position.Y }
                    }).ToList(),
                    syncedAt = DateTime.UtcNow.ToString("o")
                };

                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                string tmpPath = this.bridgePath + ".tmp";
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, this.bridgePath, true);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Bridge Sync Error: {ex.Message}", LogLevel.Error);
            }
        }

        private void ProcessActions()
        {
            try
            {
                if (!File.Exists(this.actionPath))
                    return;

                string json = File.ReadAllText(this.actionPath);
                File.Delete(this.actionPath);

                if (string.IsNullOrWhiteSpace(json))
                    return;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("actionType", out var actionType))
                    return;

                this.botManager.ProcessAction(json);

                if (actionType.GetString() == "chat")
                {
                    if (root.TryGetProperty("metadata", out var meta) &&
                        meta.TryGetProperty("message", out var msg))
                    {
                        Game1.chatBox?.addMessage(msg.GetString(), Microsoft.Xna.Framework.Color.Gold);
                        this.Monitor.Log($"Chat sent: {msg.GetString()}", LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Action Processing Error: {ex.Message}", LogLevel.Error);
            }
        }
    }
}

using MMOServer.Server;
using WebSocketSharp.Server;

namespace MMOServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=================================");
            Console.WriteLine("===   MMO Server Starting     ===");
            Console.WriteLine("=================================");
            Console.WriteLine();
            
            // [1/6] Carrega configurações JSON
            Console.WriteLine("[1/6] Loading JSON configurations...");
            ConfigManager.Instance.Initialize();
            
            // [2/6] Inicializa banco de dados
            Console.WriteLine("[2/6] Initializing database...");
            DatabaseHandler.Instance.Initialize();
            
            // [3/6] Carrega heightmap do terreno
            Console.WriteLine("[3/6] Loading terrain heightmap...");
            TerrainHeightmap.Instance.Initialize();
            
            // [4/6] Inicializa sistema de itens
            Console.WriteLine("[4/6] Initializing item system...");
            ItemManager.Instance.Initialize();
            
            // [5/6] Inicializa gerenciadores
            Console.WriteLine("[5/6] Initializing managers...");
            WorldManager.Instance.Initialize();
            
            // [6/6] Inicia servidor WebSocket
            Console.WriteLine("[6/6] Starting WebSocket server...");
            var wssv = new WebSocketServer("ws://25.22.58.214:8080");
            wssv.AddWebSocketService<GameServer>("/game");
            
            wssv.Start();
            
            Console.WriteLine();
            Console.WriteLine("=================================");
            Console.WriteLine($"✓ Server running on ws://25.22.58.214:8080/game");
            Console.WriteLine("=================================");
            Console.WriteLine();
            Console.WriteLine("Features enabled:");
            Console.WriteLine("  • JSON Configuration System");
            Console.WriteLine("  • 3D Terrain Heightmap Support");
            Console.WriteLine("  • Authoritative Movement");
            Console.WriteLine("  • Combat System (Ragnarok-style)");
            Console.WriteLine("  • Monster AI with Terrain Awareness");
            Console.WriteLine("  • Experience & Leveling");
            Console.WriteLine("  • Death & Respawn");
            Console.WriteLine("  • Item & Inventory System");
            Console.WriteLine("  • Loot System with Drop Tables");
            Console.WriteLine("  • 🆕 Area-Based Monster Spawning");
            Console.WriteLine();
            
            if (TerrainHeightmap.Instance.IsLoaded)
            {
                Console.WriteLine("Terrain Status:");
                Console.WriteLine(TerrainHeightmap.Instance.GetTerrainInfo());
            }
            else
            {
                Console.WriteLine("Terrain Status: Using flat ground (Y=0)");
                Console.WriteLine("  Export heightmap from Unity: MMO > Export Terrain Heightmap");
            }
            
            Console.WriteLine();
            Console.WriteLine("Configuration files:");
            Console.WriteLine("  • Config/monsters.json - Monster templates");
            Console.WriteLine("  • Config/classes.json - Class configurations");
            Console.WriteLine("  • Config/terrain_heightmap.json - Terrain data");
            Console.WriteLine("  • Config/items.json - Item definitions");
            Console.WriteLine("  • Config/loot_tables.json - Monster drop tables");
            Console.WriteLine("  • Config/spawn_areas.json - 🆕 Spawn area definitions");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  • 'reload' - Reload JSON configurations");
            Console.WriteLine("  • 'terrain' - Show terrain info");
            Console.WriteLine("  • 'status' - Show server status");
            Console.WriteLine("  • 'items' - Show item statistics");
            Console.WriteLine("  • 'loot' - Test loot tables");
            Console.WriteLine("  • 'monsters' - List all monsters");
            Console.WriteLine("  • 'areas' - 🆕 Show spawn area statistics");
            Console.WriteLine("  • 'respawn' - 🆕 Force respawn all dead monsters");
            Console.WriteLine("  • 'exit' - Stop the server");
            Console.WriteLine();
            
            // Loop de comandos
            bool running = true;
            while (running)
            {
                string? input = Console.ReadLine();
                
                if (string.IsNullOrEmpty(input))
                    continue;
                
                switch (input.ToLower().Trim())
                {
                    case "reload":
                        Console.WriteLine();
                        Console.WriteLine("🔄 Reloading configurations...");
                        ConfigManager.Instance.ReloadConfigs();
                        MonsterManager.Instance.ReloadFromConfig();
                        ItemManager.Instance.ReloadConfigs();
                        Console.WriteLine("✅ All configurations reloaded!");
                        Console.WriteLine();
                        break;
                    
                    case "terrain":
                        Console.WriteLine();
                        if (TerrainHeightmap.Instance.IsLoaded)
                        {
                            Console.WriteLine(TerrainHeightmap.Instance.GetTerrainInfo());
                        }
                        else
                        {
                            Console.WriteLine("Terrain: Not loaded (using flat ground)");
                            Console.WriteLine("Export heightmap from Unity: MMO > Export Terrain Heightmap");
                        }
                        Console.WriteLine();
                        break;
                    
                    case "items":
                        Console.WriteLine();
                        Console.WriteLine("📦 Item System Statistics:");
                        var players = PlayerManager.Instance.GetAllPlayers();
                        foreach (var player in players)
                        {
                            var inv = ItemManager.Instance.LoadInventory(player.character.id);
                            Console.WriteLine($"  {player.character.nome}:");
                            Console.WriteLine($"    Gold: {inv.gold}");
                            Console.WriteLine($"    Items: {inv.items.Count}/{inv.maxSlots}");
                            
                            string weaponStatus = inv.weaponId.HasValue ? inv.weaponId.Value.ToString() : "None";
                            string armorStatus = inv.armorId.HasValue ? inv.armorId.Value.ToString() : "None";
                            Console.WriteLine($"    Equipped: Weapon={weaponStatus}, Armor={armorStatus}");
                        }
                        Console.WriteLine();
                        break;
                    
                    case "loot":
                        Console.WriteLine();
                        Console.WriteLine("💰 Loot Tables:");
                        var monsters = MonsterManager.Instance.GetAllMonsters();
                        foreach (var m in monsters)
                        {
                            Console.WriteLine($"  [{m.templateId}] {m.template.name}:");
                            var testLoot = ItemManager.Instance.GenerateLoot(m.templateId);
                        }
                        Console.WriteLine();
                        break;
                    
                    case "monsters":
                        Console.WriteLine();
                        Console.WriteLine("👹 Active Monsters:");
                        var allMonsters = MonsterManager.Instance.GetAllMonsters();
                        foreach (var m in allMonsters)
                        {
                            Console.WriteLine($"  [{m.id}] {m.template.name} (Template: {m.templateId})");
                            Console.WriteLine($"      HP: {m.currentHealth}/{m.template.maxHealth}");
                            Console.WriteLine($"      Alive: {m.isAlive}, In Combat: {m.inCombat}");
                            Console.WriteLine($"      Pos: ({m.position.x:F1}, {m.position.z:F1})");
                            Console.WriteLine($"      Spawn Area: {m.spawnAreaId}");
                        }
                        Console.WriteLine();
                        break;
                    
                    case "areas":
                        Console.WriteLine();
                        Console.WriteLine("📍 Spawn Area Statistics:");
                        var areas = SpawnAreaManager.Instance.GetAllAreas();
                        var areaStats = MonsterManager.Instance.GetSpawnAreaStats();
                        
                        foreach (var area in areas)
                        {
                            Console.WriteLine($"\n  [{area.id}] {area.name}");
                            Console.WriteLine($"      Type: {area.shape}");
                            Console.WriteLine($"      Center: ({area.centerX:F1}, {area.centerZ:F1})");
                            
                            if (area.shape == "circle")
                                Console.WriteLine($"      Radius: {area.radius}m");
                            else
                                Console.WriteLine($"      Size: {area.width}x{area.length}m");
                            
                            Console.WriteLine($"      Max Slope: {area.maxSlope}°");
                            Console.WriteLine($"      Configured Spawns: {area.spawns.Count} types");
                            
                            if (areaStats.TryGetValue(area.id, out var stats))
                            {
                                Console.WriteLine($"      Active Monsters: {stats.aliveMonsters}/{stats.totalMonsters}");
                                Console.WriteLine($"      Dead: {stats.deadMonsters}, In Combat: {stats.inCombat}");
                            }
                            
                            foreach (var spawn in area.spawns)
                            {
                                Console.WriteLine($"        • {spawn.count}x {spawn.monsterName} (Respawn: {spawn.respawnTime}s)");
                            }
                        }
                        Console.WriteLine();
                        break;
                    
                    case "respawn":
                        Console.WriteLine();
                        Console.WriteLine("✨ Force respawning all dead monsters...");
                        int respawned = 0;
                        foreach (var monster in MonsterManager.Instance.GetAllMonsters())
                        {
                            if (!monster.isAlive)
                            {
                                var area = SpawnAreaManager.Instance.GetArea(monster.spawnAreaId);
                                
                                if (area != null)
                                {
                                    var newPos = SpawnAreaManager.Instance.GetRandomPositionInArea(area);
                                    
                                    if (newPos != null)
                                    {
                                        monster.position = newPos;
                                    }
                                }
                                
                                monster.Respawn();
                                TerrainHeightmap.Instance.ClampToGround(monster.position, 1f);
                                DatabaseHandler.Instance.UpdateMonsterInstance(monster);
                                respawned++;
                            }
                        }
                        Console.WriteLine($"✅ Respawned {respawned} monsters!");
                        Console.WriteLine();
                        break;
                    
                    case "exit":
                    case "quit":
                    case "stop":
                        running = false;
                        break;
                    
                    case "help":
                        Console.WriteLine();
                        Console.WriteLine("Available commands:");
                        Console.WriteLine("  reload   - Reload JSON configurations");
                        Console.WriteLine("  terrain  - Show terrain information");
                        Console.WriteLine("  status   - Show server status");
                        Console.WriteLine("  items    - Show item statistics");
                        Console.WriteLine("  loot     - Test loot generation");
                        Console.WriteLine("  monsters - List all monster instances");
                        Console.WriteLine("  areas    - Show spawn area statistics");
                        Console.WriteLine("  respawn  - Force respawn all dead monsters");
                        Console.WriteLine("  exit     - Stop the server");
                        Console.WriteLine("  help     - Show this help");
                        Console.WriteLine();
                        break;
                    
                    case "status":
                        Console.WriteLine();
                        Console.WriteLine("Server Status:");
                        Console.WriteLine($"  Players online: {PlayerManager.Instance.GetAllPlayers().Count}");
                        Console.WriteLine($"  Active monsters: {MonsterManager.Instance.GetAliveMonsters().Count}");
                        Console.WriteLine($"  Total monster instances: {MonsterManager.Instance.GetAllMonsters().Count}");
                        Console.WriteLine($"  Monster templates: {ConfigManager.Instance.MonsterConfig.monsters.Count}");
                        Console.WriteLine($"  Spawn areas: {SpawnAreaManager.Instance.GetAllAreas().Count}");
                        Console.WriteLine($"  Available classes: {ConfigManager.Instance.ClassConfig.classes.Count}");
                        
                        bool itemsLoaded = ItemManager.Instance.GetItemTemplate(1) != null;
                        Console.WriteLine($"  Item templates: {(itemsLoaded ? "Loaded" : "Not loaded")}");
                        Console.WriteLine($"  Terrain loaded: {(TerrainHeightmap.Instance.IsLoaded ? "Yes" : "No")}");
                        Console.WriteLine();
                        break;
                    
                    default:
                        Console.WriteLine($"Unknown command: {input}");
                        Console.WriteLine("Type 'help' for available commands");
                        break;
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("Shutting down server...");
            WorldManager.Instance.Shutdown();
            wssv.Stop();
            Console.WriteLine("Server stopped successfully.");
        }
    }
}